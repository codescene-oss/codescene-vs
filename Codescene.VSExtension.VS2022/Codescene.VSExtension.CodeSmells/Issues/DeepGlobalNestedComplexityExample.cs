using System;

namespace Codescene.VSExtension.CodeSmells.Issues
{
    // Global settings used throughout the application
    static class GlobalSettings
    {
        public static bool FeatureAEnabled = true;
        public static bool FeatureBEnabled = true;
        public static bool DebugMode = true;
    }

    // Global utilities class with static variables and methods
    static class GlobalUtilities
    {
        public static int GlobalCounter = 0;
        public static string SomeGlobalString = "Hello from the global scope!";

        public static bool CheckCondition(int value)
        {
            // Logic that depends on global settings
            return GlobalSettings.FeatureBEnabled && (value % 2 == 0);
        }
    }

    class DeepGlobalNestedComplexityExample
    {
        public void ComplexMethod(int value)
        {
            // 1. Nested if using a global setting
            if (GlobalSettings.FeatureAEnabled)
            {
                // 2. Another condition check
                if (value > 10)
                {
                    // 3. For loop
                    for (int i = 0; i < value; i++)
                    {
                        // 4. Condition inside the for loop
                        if (GlobalSettings.DebugMode)
                        {
                            Console.WriteLine($"[Debug] Iteration {i}, Value = {value}");
                            // 5. Another if statement
                            if (GlobalUtilities.SomeGlobalString.Contains("Hello"))
                            {
                                // 6. While loop
                                while (GlobalUtilities.GlobalCounter < i)
                                {
                                    // 7. Condition inside the while loop
                                    if (GlobalUtilities.CheckCondition(i))
                                    {
                                        Console.WriteLine("Condition met – incrementing GlobalCounter.");
                                        GlobalUtilities.GlobalCounter++;
                                    }
                                    else
                                    {
                                        Console.WriteLine("Condition not met – resetting GlobalCounter.");
                                        GlobalUtilities.GlobalCounter = 0;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Fallback when DebugMode is not enabled
                            if (GlobalUtilities.CheckCondition(i))
                            {
                                Console.WriteLine("Operation in non-debug mode, condition was true.");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Value must be greater than 10 to proceed with complex logic.");
                }
            }
            else
            {
                Console.WriteLine("Feature A is not enabled.");
            }
        }
    }
}