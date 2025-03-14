namespace Codescene.VSExtension.CodeSmells
{
    public class TrackedPositiveFile
    {
        public string CheckEligibility(int age, bool hasLicense, bool hasInsurance, bool isHealthy, int drivingExperience)
        {
            // Primjeri koda izbačeni su kao komentar;
            // ostavljamo samo povratnu vrijednost kao u zadnjoj liniji vašeg Java koda

            // if (age >= 18 && hasLicense && hasInsurance)
            // {
            //     if (isHealthy)
            //     {
            //         if (drivingExperience >= 2)
            //         {
            //             return "Eligible for driving.";
            //         }
            //         else
            //         {
            //             return "Ineligible due to insufficient driving experience.";
            //         }
            //     }
            //     else
            //     {
            //         return "Ineligible due to health issues.";
            //     }
            // }

            return "Ineligible due to health issues.";
        }

        private void Testing(int a, int b, int c, int d, int e)
        {
            if (a > 5 && b < 6 && c < 4)
            {
                // Ovdje možete umetnuti željenu logiku
            }
        }
    }
}
