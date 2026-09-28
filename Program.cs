// // ** ** ** ** ** ** 기본 Monitor ** ** ** ** ** ** 

// class Program
// {
//   static int data = 0;
//   static readonly object lockObject = new object();

//   static void Main()
//   {
//     void DoWork()
//     {
//       for (int i = 0; i < 10; i++)
//       {
//         Monitor.Enter(lockObject);
//         Thread.Sleep(1);
//         data++;
//         Monitor.Exit(lockObject);
//       }
//     }

//     List<Thread> threads = new List<Thread>();
//     for (int i = 0; i < 10; i++)
//     {
//       Thread thread = new Thread(DoWork);
//       threads.Add(thread);
//       thread.Start();
//     }

//     threads.ForEach(t => t.Join());

//     Console.WriteLine(data);

//     Console.ReadKey();
//   }
// }


// ** ** ** ** ** ** Monitor.TryEnter ** ** ** ** ** ** 

using System;
using System.Threading;

class Program
{
    private static object _lock = new object();

    static void Main()
    {
        Thread thread1 = new Thread(DoWork);
        Thread thread2 = new Thread(DoWork);

        thread1.Start();
        thread2.Start();

        thread1.Join();
        thread2.Join();

        Console.WriteLine("모든 쓰래드 완료");
        Console.ReadKey();
    }

    static void DoWork()
    {
        if (Monitor.TryEnter(_lock, TimeSpan.FromMilliseconds(1500)))
        {
            try
            {
                Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId} 번 쓰래드 잠금 획득");
                Thread.Sleep(1000); // Simulate work
            }
            finally
            {
                Monitor.Exit(_lock);
                Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId} 번 쓰래드 잠금 해제");
            }
        }
        else
        {
            Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId} 번 쓰래드가 잠금을 획득하지 못했습니다.");
        }
    }
}

// // ** ** ** ** ** ** Monitor.Wait와 Monitor.Pulse ** ** ** ** ** ** 

// class Program
// {
//   private static object _lock = new object();
//   private static bool _isReady = false;

//   static void Main()
//   {
//     Thread producer = new Thread(Producer);
//     Thread consumer = new Thread(Consumer);

//     consumer.Start();
//     Thread.Sleep(500); // 소비자가 먼저 실행되게 잠시 대기
//     producer.Start();

//     producer.Join();
//     consumer.Join();

//     Console.WriteLine("모든 스레드가 완료되었습니다.");
//     Console.ReadKey();
//   }

//   static void Producer()
//   {
//     lock (_lock)    
//     {
//       Console.WriteLine("생산자가 데이터를 준비 중입니다...");
//       Thread.Sleep(2000); // 작업 시뮬레이션
//       _isReady = true;
//       Console.WriteLine("생산자가 데이터 준비 완료를 알립니다.");
//       //Monitor.Pulse(_lock); // 대기 중인 스레드에 알림
//     }
//   }

//   static void Consumer()
//   {
//     lock (_lock)
//     {
//       while (!_isReady)
//       {
//         Console.WriteLine("소비자가 데이터를 기다리고 있습니다...");
//         Monitor.Wait(_lock); // 신호 대기
//       }
//       Console.WriteLine("소비자가 데이터를 받았습니다.");
//     }
//   }
// }