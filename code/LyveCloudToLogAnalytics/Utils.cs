using Amazon.S3;
using System;
using System.Collections.Generic;
using System.Text;

namespace LyveCloudToLogAnalytics
{
    /*
     * this function holds different utils such:
     * connection to lyve cloud via S3 client
     * return function of app settings (bucket name, log name etc..)
     */
    public static class Utils
    {

        /* Connecting to Lyve Cloud using AWS SDK*/
        public static AmazonS3Client ConnectLyveS3()
        {
            string UrlKey = Environment.GetEnvironmentVariable("LyveUrl");
            string AccessKey = Environment.GetEnvironmentVariable("LyveAccessKey");
            string SecretKey = Environment.GetEnvironmentVariable("LyveSecretKey");

            AmazonS3Config config = new AmazonS3Config();
            config.ServiceURL = UrlKey;

            return new AmazonS3Client(AccessKey,SecretKey,config);
        }

        /* returns bucket name as set in app settings */
        public static string GetBucketName()
        {
            return Environment.GetEnvironmentVariable("BucketName");
        }

        /* returns start time for scanning, as set in app settings */

        public static string GetStartTime()
        {
            return Environment.GetEnvironmentVariable("StartTime");
        }

        /* returns log name inside of Log Analytics to which the lyve logs should be uploaded to */
        public static string GetLogName()
        {
            return Environment.GetEnvironmentVariable("LogAnalyticsLogName");
        }

    }
}
