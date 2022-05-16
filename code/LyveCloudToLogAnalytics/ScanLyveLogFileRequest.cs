using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyveCloudToLogAnalytics
{
    public class ScanLyveLogFileRequest
    {
        public string FileUrl { get; set; }
        public string BucketName { get; set; }
    }
}
