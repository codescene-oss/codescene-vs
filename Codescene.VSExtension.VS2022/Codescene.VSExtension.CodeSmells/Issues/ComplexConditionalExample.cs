namespace Codescene.VSExtension.CodeSmells.Issues
{
    class ComplexConditionalExample
    {
        public string ComplexConditional(int value1, int value2)
        {
            if (value1 > 10 && (value2 < 5 || value2 == 100))
            {
                return "Condition A met";
            }
            else if (value1 <= 10 && value2 != 100)
            {
                return "Condition B met";
            }
            else
            {
                return "None of the above";
            }
        }
    }
}
