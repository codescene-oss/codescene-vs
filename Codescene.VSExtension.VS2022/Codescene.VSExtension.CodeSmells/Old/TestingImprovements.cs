using System;

namespace Codescene.VSExtension.CodeSmells
{
    public class TestingImprovements
    {
        //Comment method body to fix deep,nested complexity smell
        public void ProcessEverything(string input1, string input2, string input3, string input4, string input5, string input6)
        {
            if (input1 != null)
            {
                if (input2.Length > 5)
                {
                    if (input3.StartsWith("A"))
                    {
                        for (int i = 0; i < input4.Length; i++)
                        {
                            if (input5.Contains("error"))
                            {
                                try
                                {
                                    if (input6.Equals("specialCase"))
                                    {
                                        Console.WriteLine("Processing special case...");
                                        HelperMethod(input1, input2);
                                        HelperMethod2(input3, input4);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Normal processing...");
                                        HelperMethod3(input5, input6, input1);
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine("Exception caught: " + e.Message);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Input1 was null, cannot process.");
            }
        }

        private void HelperMethod(string arg1, string arg2)
        {
            if (arg1.Length > 10)
            {
                Console.WriteLine("Long argument: " + arg1);
                NestedHelperMethod(arg2);
            }
            else
            {
                Console.WriteLine("Short argument.");
            }
        }

        private void HelperMethod2(string arg1, string arg2)
        {
            for (int i = 0; i < arg1.Length; i++)
            {
                for (int j = 0; j < arg2.Length; j++)
                {
                    if (arg1[i] == arg2[j])
                    {
                        Console.WriteLine("Common character: " + arg1[i]);
                    }
                }
            }
        }

        private void HelperMethod3(string arg1, string arg2, string arg3)
        {
            Console.WriteLine("Processing with args: " + arg1 + ", " + arg2 + arg3);
            if (arg1.Contains("process"))
            {
                if (arg2.Equals("complete"))
                {
                    Console.WriteLine("Process complete.");
                }
                else
                {
                    Console.WriteLine("Incomplete process.");
                }
            }
        }

        private void NestedHelperMethod(string arg)
        {
            if (arg.Length > 5)
            {
                for (int i = 0; i < 10; i++)
                {
                    if (i % 2 == 0)
                    {
                        Console.WriteLine("Even index: " + i);
                    }
                    else
                    {
                        if (arg.StartsWith("B"))
                        {
                            Console.WriteLine("Starts with B at odd index.");
                        }
                    }
                }
            }
        }

        private void EvenMoreStrings(string arg)
        {
            if (arg.Length > 5)
            {
                for (int i = 0; i < 10; i++)
                {
                    if (i % 2 == 0)
                    {
                        Console.WriteLine("Even index: " + i);
                    }
                    else
                    {
                        if (arg.StartsWith("B"))
                        {
                            Console.WriteLine("Starts with B at odd index.");
                        }
                    }
                }
            }
        }

        //Comment this method out to fix bumpy road ahead, code duplication and deep, nested complexity
        private void Ev3enMoreStrings(string arg)
        {
            if (arg.Length > 5)
            {
                for (int i = 0; i < 10; i++)
                {
                    if (i % 2 == 0)
                    {
                        Console.WriteLine("Even index: " + i);
                    }
                    else
                    {
                        if (arg.StartsWith("B"))
                        {
                            Console.WriteLine("Starts with B at odd index.");
                        }
                    }
                }
            }
        }

        public static void Main(string[] args)
        {
            TestingImprovements example = new TestingImprovements();
            example.ProcessEverything("Alpha", "Bravo", "Charlie", "Delta", "error", "specialCase");
        }
    }
}
