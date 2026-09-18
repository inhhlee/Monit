"""Real Mosquitto COM-03 verification. Requires Docker and restored .NET dependencies.

Creates only uniquely named disposable test containers; never touches compose services/data.
Uses a TCP proxy to drop broker responses without changing host firewall configuration.
"""
import argparse
import hashlib
import json
import os
import platform
import select
import shutil
import socket
import subprocess
import sys
import threading
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path

from verify_test_results import verify as verify_trx

ROOT = Path(__file__).resolve().parents[1]
IMAGE = 'eclipse-mosquitto:2.1.2-alpine'
HIDDEN = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0


def utc():
    return datetime.now(timezone.utc).isoformat()


def require(condition, message):
    if not condition:
        raise AssertionError(message)


class Run:
    def __init__(self, docker, output):
        self.docker, self.output = docker, output
        self.commands = []
        self.manifest = {'feature': 'COM-03', 'spec_revision': 2, 'started_at': utc(),
                         'result': 'running', 'checks': {}, 'environment': {}}

    def command(self, args, log, timeout=60):
        result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, encoding='utf-8',
                                errors='replace', timeout=timeout, creationflags=HIDDEN)
        (self.output / log).write_text(result.stdout + result.stderr, encoding='utf-8')
        self.commands.append({'at': utc(), 'args': args, 'exit_code': result.returncode, 'log': log})
        require(result.returncode == 0, f'Command failed ({result.returncode}), see {log}')
        return result.stdout

    def check(self, index, actual, artifacts):
        self.manifest['checks'][str(index)] = {'result': 'passed', 'executed_at': utc(),
                                               'actual': actual, 'artifacts': artifacts}
        print(f'PASS verification {index}: {actual}', flush=True)

    def save(self):
        self.manifest['finished_at'] = utc()
        self.manifest['commands'] = self.commands
        (self.output / 'result.json').write_text(json.dumps(self.manifest, indent=2, ensure_ascii=False), encoding='utf-8')


class Broker:
    def __init__(self, run, label):
        self.run, self.label = run, label
        self.name = f'amr-com03-{label}-{uuid.uuid4().hex[:10]}'
        self.created = False

    def __enter__(self):
        config = self.run.output / 'mosquitto.conf'
        try:
            self.run.command([self.run.docker, 'run', '-d', '--name', self.name,
                              '--label', 'amr.verification=COM-03', '-p', '127.0.0.1::1883',
                              '--mount', f'type=bind,source={config},target=/mosquitto/config/mosquitto.conf,readonly',
                              IMAGE], f'{self.label}-create.log')
            self.created = True
            address = self.run.command([self.run.docker, 'port', self.name, '1883/tcp'], f'{self.label}-port.log').strip()
            self.port = int(address.rsplit(':', 1)[1])
            deadline = time.monotonic() + 10
            while True:
                try:
                    with socket.create_connection(('127.0.0.1', self.port), timeout=0.3): break
                except OSError:
                    if time.monotonic() >= deadline: raise
                    time.sleep(0.1)
            return self
        except BaseException:
            if self.created: self.__exit__(*sys.exc_info())
            raise

    def logs(self):
        return self.run.command([self.run.docker, 'logs', self.name], f'{self.label}-broker.log')

    def stop(self):
        self.run.command([self.run.docker, 'stop', '--time', '5', self.name], f'{self.label}-stop.log', timeout=15)

    def __exit__(self, *args):
        if self.created:
            try: self.logs()
            finally:
                self.run.command([self.run.docker, 'rm', '-f', self.name], f'{self.label}-cleanup.log')


class Host:
    def __init__(self, run, label, port, duration):
        self.run, self.label = run, label
        self.records, self.lock = [], threading.Lock()
        self.args = ['dotnet', str(ROOT / 'src/Amr.Communication.Console/bin/Debug/net10.0/Amr.Communication.Console.dll'),
                     '--host', '127.0.0.1', '--port', str(port), '--keepalive-seconds', '2',
                     '--timeout-seconds', '3', '--duration-seconds', str(duration)]

    def __enter__(self):
        environment = dict(os.environ)
        environment.pop('AMR_MQTT_USERNAME', None)
        environment.pop('AMR_MQTT_PASSWORD', None)
        self.process = subprocess.Popen(self.args, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                        text=True, encoding='utf-8', errors='replace', env=environment, creationflags=HIDDEN)
        self.started = time.monotonic()
        self.reader = threading.Thread(target=self.read, daemon=True)
        self.reader.start()
        return self

    def read(self):
        with (self.run.output / f'{self.label}-client.log').open('w', encoding='utf-8') as log:
            for line in self.process.stdout:
                log.write(line)
                log.flush()
                try: record = json.loads(line)
                except json.JSONDecodeError: continue
                with self.lock: self.records.append((time.monotonic(), record))

    def wait_state(self, state, timeout=5):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            with self.lock:
                for at, record in self.records:
                    if record.get('State') == state: return at, record
            if self.process.poll() is not None:
                self.reader.join(timeout=1)
                with self.lock:
                    for at, record in self.records:
                        if record.get('State') == state: return at, record
                break
            time.sleep(0.02)
        raise AssertionError(f'{self.label}: expected state {state}; see client log')

    def finish(self, expected, timeout=10):
        code = self.process.wait(timeout=timeout)
        self.reader.join(timeout=2)
        self.run.commands.append({'at': utc(), 'args': self.args, 'exit_code': code, 'log': f'{self.label}-client.log'})
        require(code == expected, f'{self.label}: expected exit {expected}, got {code}')
        return list(self.records)

    def __exit__(self, *args):
        if self.process.poll() is None:
            self.process.kill()
            self.process.wait(timeout=5)
        self.reader.join(timeout=2)
        self.process.stdout.close()


class ResponseDropProxy:
    """Test-only byte relay. Keeps TCP open and drops only server->client data on demand."""
    def __init__(self, target_port):
        self.target_port = target_port
        self.drop, self.stop = threading.Event(), threading.Event()
        self.dropped_bytes = 0
        self.error = None
        self.listener = socket.socket()
        self.listener.bind(('127.0.0.1', 0))
        self.listener.listen(1)
        self.listener.settimeout(0.2)
        self.port = self.listener.getsockname()[1]

    def __enter__(self):
        self.worker = threading.Thread(target=self.relay, daemon=True)
        self.worker.start()
        return self

    def relay(self):
        try:
            while not self.stop.is_set():
                try:
                    client, _ = self.listener.accept()
                    break
                except socket.timeout: continue
            else: return
            with client, socket.create_connection(('127.0.0.1', self.target_port), timeout=3) as server:
                client.settimeout(2)
                server.settimeout(2)
                while not self.stop.is_set():
                    readable, _, _ = select.select([client, server], [], [], 0.1)
                    for source in readable:
                        data = source.recv(65536)
                        if not data: return
                        if source is server and self.drop.is_set(): self.dropped_bytes += len(data)
                        else: (server if source is client else client).sendall(data)
        except OSError as error:
            if not self.stop.is_set(): self.error = type(error).__name__

    def __exit__(self, *args):
        self.stop.set()
        self.worker.join(timeout=4)
        self.listener.close()


def verify(run):
    env = run.manifest['environment']
    env.update(os=platform.platform(), python=sys.version, broker_image=IMAGE, mqttnet='5.2.0.1603',
               protocol='MQTT 5 / TCP', authentication='anonymous, isolated localhost only',
               keepalive_seconds=2, timeout_seconds=3, core_amr_test_doubles='none; lifecycle fake only in unit tests',
               response_block='test TCP proxy drops broker-to-client bytes; no firewall changes')
    env['code_commit'] = run.command(['git', 'rev-parse', 'HEAD'], 'code-version.log').strip()
    env['changes'] = run.command(['git', 'status', '--short'], 'code-changes.log')
    env['docker'] = run.command([run.docker, 'version', '--format', '{{.Server.Version}}'], 'docker-version.log').strip()
    env['image_id'] = run.command([run.docker, 'image', 'inspect', IMAGE, '--format', '{{.Id}}'], 'image-id.log').strip()
    run.command(['dotnet', 'build', 'AmrControlSystem.slnx', '--no-restore'], 'build.log')
    source_files = [p for folder in ('src', 'tests', 'scripts') for p in (ROOT / folder).rglob('*')
                    if p.is_file() and not {'bin', 'obj', '__pycache__'}.intersection(p.parts)
                    and p.suffix in {'.cs', '.csproj', '.py'}]
    env['source_sha256'] = {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(source_files)}
    (run.output / 'mosquitto.conf').write_text('listener 1883 0.0.0.0\nallow_anonymous true\npersistence false\nlog_dest stdout\nlog_type all\n', encoding='utf-8')

    with Broker(run, 'healthy') as broker:
        with Host(run, 'idle', broker.port, 6) as host:
            connected_at, _ = host.wait_state('Connected')
            run.check(1, f'Client connected to actual Mosquitto at 127.0.0.1:{broker.port}', ['idle-client.log', 'healthy-broker.log', 'mosquitto.conf'])
            events = host.finish(0, timeout=10)
            disconnect_at = next(at for at, e in events if e.get('State') == 'Disconnecting')
            observed = disconnect_at - connected_at
            require(observed >= 4, 'Idle observation shorter than 2 * KeepAlive')
            client_id = next(e['ClientId'] for _, e in events if e.get('Event') == 'Configuration')
            log = broker.logs()
            require(f'New client connected' in log and client_id in log, 'Missing broker connection evidence')
            require(f'Received PINGREQ from {client_id}' in log and f'Sending PINGRESP to {client_id}' in log, 'Missing PINGREQ/PINGRESP')
            require('Received PUBLISH' not in log and 'Received SUBSCRIBE' not in log, 'Unexpected business traffic')
            run.check(2, f'Idle connection maintained for {observed:.3f}s with PINGREQ/PINGRESP and no business messages', ['idle-client.log', 'healthy-broker.log'])
        # Restart the real host without source edits or data manipulation.
        with Host(run, 'restart', broker.port, 1) as host:
            host.wait_state('Connected')
            host.finish(0)

    with Broker(run, 'shutdown') as broker:
        with Host(run, 'shutdown', broker.port, 30) as host:
            host.wait_state('Connected')
            stopped_at = time.monotonic()
            broker.stop()
            detected_at, event = host.wait_state('Faulted')
            normal_delay = detected_at - stopped_at
            require(event['Reason'] == 'ConnectionLost' and normal_delay <= 5, 'Broker shutdown detection outside 5s')
            host.finish(3)
            require('terminating' in broker.logs(), 'Missing orderly Mosquitto termination log')

    with Broker(run, 'blackhole') as broker, ResponseDropProxy(broker.port) as proxy:
        with Host(run, 'blackhole', proxy.port, 30) as host:
            host.wait_state('Connected')
            blocked_at = time.monotonic()
            proxy.drop.set()
            detected_at, event = host.wait_state('Faulted', timeout=8)
            blocked_delay = detected_at - blocked_at
            require(event['Reason'] == 'ConnectionLost' and blocked_delay <= 8, 'Response loss detection outside 8s')
            require(proxy.dropped_bytes > 0 and proxy.error is None, 'Proxy did not drop responses cleanly')
            host.finish(3)
        with Host(run, 'broker-still-alive', broker.port, 1) as probe:
            probe.wait_state('Connected')
            probe.finish(0)
        (run.output / 'fault-timing.json').write_text(json.dumps(dict(normal_shutdown_seconds=normal_delay,
            response_drop_seconds=blocked_delay, dropped_bytes=proxy.dropped_bytes, broker_still_alive=True,
            response_drop_limit_seconds=8, normal_shutdown_limit_seconds=5), indent=2), encoding='utf-8')
        run.check(3, f'Normal shutdown detected in {normal_delay:.3f}s; blocked responses in {blocked_delay:.3f}s; broker remained alive',
                  ['shutdown-client.log', 'shutdown-broker.log', 'blackhole-client.log', 'blackhole-broker.log', 'broker-still-alive-client.log', 'fault-timing.json'])

    # Reserve an unused port without listening so no other process can take it during the check.
    with socket.socket() as unused:
        unused.bind(('127.0.0.1', 0))
        with Host(run, 'unavailable', unused.getsockname()[1], 1) as host:
            fault_at, event = host.wait_state('Faulted')
            events = host.finish(2)
            require(event['Reason'] == 'ConnectFailed' and event.get('Error'), 'Initial failure missing reason')
            require(not any(e.get('State') == 'Connected' for _, e in events), 'Unavailable broker reported connected')
            require(fault_at - host.started <= 5, 'Initial failure exceeded 5s')
            run.check(4, 'Unavailable broker produced ConnectFailed with reason, no Connected state and controlled exit 2', ['unavailable-client.log'])

    run.command(['dotnet', 'test', 'tests/Amr.Communication.Tests/Amr.Communication.Tests.csproj', '--no-build', '--no-restore',
                 '--logger', 'trx;LogFileName=unit.trx', '--results-directory', str(run.output)], 'unit.log')
    count = verify_trx(run.output / 'unit.trx')
    run.check(5, f'{count} automatic tests passed; real Mosquitto checks 1-4 and host restart passed', ['build.log', 'unit.log', 'unit.trx', 'restart-client.log', 'result.json'])
    run.manifest['result'] = 'passed'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--docker', default=os.environ.get('AMR_DOCKER') or shutil.which('docker'))
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    if not args.docker:
        bundled = Path.home() / 'AppData/Local/Programs/DockerDesktop/resources/bin/docker.exe'
        if bundled.is_file(): args.docker = str(bundled)
    if not args.docker:
        parser.error('Docker executable required; use --docker or AMR_DOCKER')
    output = (args.output or ROOT / 'evidence/COM-03' / (datetime.now().strftime('%Y%m%dT%H%M%S') + '-' + uuid.uuid4().hex[:6])).resolve()
    output.mkdir(parents=True, exist_ok=False)
    run = Run(args.docker, output)
    try:
        verify(run)
        return 0
    except Exception as error:
        run.manifest.update(result='failed', error=f'{type(error).__name__}: {error}')
        print(f'FAIL: {error}', file=sys.stderr)
        return 1
    finally:
        run.save()
        print(f'Evidence: {output}', flush=True)


if __name__ == '__main__':
    sys.exit(main())
