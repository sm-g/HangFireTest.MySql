using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.MySql;
using Hangfire.Server;
using Serilog;

namespace HangFireTest.MySql
{
    internal class MyServices
    {
        [MyFilter]
        [AutomaticRetry(Attempts = 0)]
        [LatencyTimeout(30)]
        [DisplayName("My name is: {0}")] // only for dashboard
        public static void Wait500AndCheckToken(string name, PerformContext context)
        {
            Console.WriteLine("Begin " + name);

            var jobCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken.ShutdownToken);

            for (int i = 0; i < 1000; i++)
            {
                Console.WriteLine(i);

                Thread.Sleep(500);

                using (var stepsCts = new CancellationTokenSource())
                {
                    try
                    {
                        if (jobCts.IsCancellationRequested)
                            throw new DivideByZeroException();

                        throw new InvalidOperationException();
                    }
                    catch (Exception)
                    {
                        stepsCts.Cancel();
                        Console.WriteLine("Bye!");
                        return;
                    }
                }
            }
            Console.WriteLine("Finished!");
        }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss}|{ThreadId:00}|{Level:u3}{EventId} {SourceContext}{Scope}|{Message}{NewLine}{Exception}")
                .WriteTo.Trace()
                .WriteTo.File(".\\..\\..\\logs\\.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:HH:mm:ss}|{ThreadId:00}|{Level:u3}{EventId} {SourceContext}{Scope}|{Message}{NewLine}{Exception}")
                .Enrich.FromLogContext()
                .MinimumLevel.Verbose()
                .CreateLogger();
            GlobalConfiguration.Configuration
                //.UseColouredConsoleLogProvider() // comment to use Serilog
                .UseStorage(new MySqlStorage("server=localhost;port=3306;database=hf_test;uid=root;password=1111;Convert Zero Datetime=True;Allow User Variables=True", new MySqlStorageOptions()
                {
                    QueuePollInterval = TimeSpan.FromSeconds(5)
                }))
                ;

            var options = new BackgroundJobServerOptions
            {
                Queues = new[] { "critical", "default" },
                WorkerCount = 2,
                SchedulePollingInterval = TimeSpan.FromSeconds(2),
                ShutdownTimeout = TimeSpan.FromSeconds(3),
                ServerTimeout = TimeSpan.FromMinutes(1)
            };

            using (new BackgroundJobServer(options))
            {
                //var result = BackgroundJob.Requeue("2");
                var id = BackgroundJob.Enqueue(() => MyServices.Wait500AndCheckToken("my job", null));
                Console.WriteLine("press to delete job");
                Console.ReadLine();
                //Console.WriteLine("Going to delete");
                //var wasDeleted = BackgroundJob.Delete(id);
                //Console.WriteLine(wasDeleted);
                //Console.ReadLine();
            }

            using (new BackgroundJobServer(options))
            {
                var count = 1;

                while (true)
                {
                    var command = Console.ReadLine();

                    if (command == null || command.Equals("stop", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (command.StartsWith("add", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var workCount = int.Parse(command.Substring(4));
                            for (var i = 0; i < workCount; i++)
                            {
                                var number = i;
                                BackgroundJob.Enqueue<Services>(x => x.Random(number));
                            }
                            Console.WriteLine("Jobs enqueued.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }

                    if (command.StartsWith("async", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var workCount = int.Parse(command.Substring(6));
                            for (var i = 0; i < workCount; i++)
                            {
                                BackgroundJob.Enqueue<Services>(x => x.Async(CancellationToken.None));
                            }
                            Console.WriteLine("Jobs enqueued.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }

                    if (command.StartsWith("static", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var workCount = int.Parse(command.Substring(7));
                            for (var i = 0; i < workCount; i++)
                            {
                                BackgroundJob.Enqueue(() => Console.WriteLine("Hello, {0}!", "world"));
                            }
                            Console.WriteLine("Jobs enqueued.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }

                    if (command.StartsWith("error", StringComparison.OrdinalIgnoreCase))
                    {
                        var workCount = int.Parse(command.Substring(6));
                        for (var i = 0; i < workCount; i++)
                        {
                            BackgroundJob.Enqueue<Services>(x => x.Error());
                        }
                    }

                    if (command.StartsWith("args", StringComparison.OrdinalIgnoreCase))
                    {
                        var workCount = int.Parse(command.Substring(5));
                        for (var i = 0; i < workCount; i++)
                        {
                            BackgroundJob.Enqueue<Services>(x => x.Args(Guid.NewGuid().ToString(), 14442, DateTime.UtcNow));
                        }
                    }

                    if (command.StartsWith("custom", StringComparison.OrdinalIgnoreCase))
                    {
                        var workCount = int.Parse(command.Substring(7));
                        for (var i = 0; i < workCount; i++)
                        {
                            BackgroundJob.Enqueue<Services>(x => x.Custom(
                                new Random().Next(),
                                new[] { "Hello", "world!" },
                                new Services.CustomObject { Id = 123 },
                                DayOfWeek.Friday
                                ));
                        }
                    }

                    if (command.StartsWith("fullargs", StringComparison.OrdinalIgnoreCase))
                    {
                        var workCount = int.Parse(command.Substring(9));
                        for (var i = 0; i < workCount; i++)
                        {
                            BackgroundJob.Enqueue<Services>(x => x.FullArgs(
                                false,
                                123,
                                'c',
                                DayOfWeek.Monday,
                                "hello",
                                new TimeSpan(12, 13, 14),
                                new DateTime(2012, 11, 10),
                                new Services.CustomObject { Id = 123 },
                                new[] { "1", "2", "3" },
                                new[] { 4, 5, 6 },
                                new long[0],
                                null,
                                new List<string> { "7", "8", "9" }));
                        }
                    }

                    if (command.StartsWith("in", StringComparison.OrdinalIgnoreCase))
                    {
                        var seconds = int.Parse(command.Substring(2));
                        var number = count++;
                        BackgroundJob.Schedule<Services>(x => x.Random(number), TimeSpan.FromSeconds(seconds));
                    }

                    if (command.StartsWith("cancelable", StringComparison.OrdinalIgnoreCase))
                    {
                        var iterations = int.Parse(command.Substring(11));
                        BackgroundJob.Enqueue<Services>(x => x.Cancelable(iterations, JobCancellationToken.Null));
                    }

                    if (command.StartsWith("delete", StringComparison.OrdinalIgnoreCase))
                    {
                        var workCount = int.Parse(command.Substring(7));
                        for (var i = 0; i < workCount; i++)
                        {
                            var jobId = BackgroundJob.Enqueue<Services>(x => x.EmptyDefault());
                            BackgroundJob.Delete(jobId);
                        }
                    }

                    if (command.StartsWith("fast", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var workCount = int.Parse(command.Substring(5));
                            Parallel.For(0, workCount, i =>
                            {
                                if (i % 2 == 0)
                                {
                                    BackgroundJob.Enqueue<Services>(x => x.EmptyCritical());
                                }
                                else
                                {
                                    BackgroundJob.Enqueue<Services>(x => x.EmptyDefault());
                                }
                            });
                            Console.WriteLine("Jobs enqueued.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }

                    if (command.StartsWith("continuations", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteString("Hello, Hangfire continuations!");
                    }
                }
            }

            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }

        public static void WriteString(string value)
        {
            var lastId = BackgroundJob.Enqueue<Services>(x => x.Write(value[0]));

            for (var i = 1; i < value.Length; i++)
            {
                lastId = BackgroundJob.ContinueWith<Services>(lastId, x => x.Write(value[i]));
            }

            BackgroundJob.ContinueWith<Services>(lastId, x => x.WriteBlankLine());
        }
    }
}