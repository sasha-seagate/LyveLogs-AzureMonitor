using System.Threading.Tasks;

namespace LyveCloudToLogAnalytics
{
    public interface IAzureLogAnalyticsClient
    {
        Task WriteLog(string logName, string logMessage);
    }
}