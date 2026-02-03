namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface ICliObjectScoreCreator
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="oldScore">Raw base64 encoded score</param>
        /// <param name="newScore">Raw base64 encoded score</param>
        /// <returns></returns>
        string Create(string oldScore, string newScore);
    }
}
