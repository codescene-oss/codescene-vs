namespace Codescene.VSExtension.Core.Models.ReviewResult
{
    public class Details
    {
        public float Ccmean { get; set; }
        public int Mainbodycc { get; set; }
        public int Linesinfile { get; set; }
        public object[] Complexfunctions { get; set; }
        public float Ccmedian { get; set; }
        public Nested Nested { get; set; }
        public object[] Excesslongfunctions { get; set; }
        public object[] Complexconditionals { get; set; }
        public string Longestfnlocname { get; set; }
        public int Nfunctions { get; set; }
        public int Activecodesize { get; set; }
        public Bumps Bumps { get; set; }
        public int Longestfnloc { get; set; }
        public int Cohesion { get; set; }
        public int Cloneratio { get; set; }
        public FnArgs Fnargs { get; set; }
        public int Ccmax { get; set; }
        public int Nclones { get; set; }
        public Congestion Congestion { get; set; }
        public float Medianfnloc { get; set; }
        public string Ccmaxname { get; set; }
        public int Cctotal { get; set; }
    }
}
