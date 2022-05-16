using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Util;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LyveCloudToLogAnalytics
{
    /*
     * this function triggers each time when log is added to a queue
     * it downloads the log file from the bucket (which is an archive)
     * extracts the content of archive file
     * uploads it to Log Analytics line by line (each line is an enrty)
     */
    public class DownloadAndIngestLogFile
    {
        AmazonS3Client S3Client;

        string _logName = Utils.GetLogName();

        IAzureLogAnalyticsClient _logAnalyticsClient;

        public DownloadAndIngestLogFile(IAzureLogAnalyticsClient logAnalyticsClient)
        {
            // retrieve Log Analytics client
            _logAnalyticsClient = logAnalyticsClient;
        }


        [FunctionName("DownloadAndIngestLogFile")]
        public async Task Run([QueueTrigger("logFiles", Connection = "AzureWebJobsStorage")] ScanLyveLogFileRequest fileToScanRequest, ILogger log)
        {

            try
            {
                // connect to lyve cloud S3
                S3Client = Utils.ConnectLyveS3();

                var s3Object = await S3Client.GetObjectAsync(fileToScanRequest.BucketName, fileToScanRequest.FileUrl);

                var tempFile = Path.GetTempFileName();
                var extractedDir = tempFile + "_e";
                Directory.CreateDirectory(extractedDir);
                string extractedFile = Path.Combine(extractedDir, Path.GetFileName(fileToScanRequest.FileUrl));
                File.Delete(tempFile);
                await s3Object.WriteResponseStreamToFileAsync(tempFile, false, CancellationToken.None);

                ExtractLogFile(tempFile, extractedFile);

                using (var file = new StreamReader(extractedFile))
                {
                    var line = "";
                    while ((line = await file.ReadLineAsync()) != null)
                    {
                        
                            await _logAnalyticsClient.WriteLog(_logName, line);
                        

                    }
                }

                Directory.Delete(extractedDir, true);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error while processing {file} {bucket}", fileToScanRequest.FileUrl, fileToScanRequest.BucketName);
            }

        }

        /* extracts archive file*/
        private static void ExtractLogFile(string compressedFile, string extractedFile)
        {
            byte[] dataBuffer = new byte[4096];
            using (Stream fs = new FileStream(compressedFile, FileMode.Open, FileAccess.Read))
            {
                using (GZipInputStream gzipStream = new GZipInputStream(fs))
                {

                    using (FileStream fsOut = File.Create(extractedFile))
                    {
                        StreamUtils.Copy(gzipStream, fsOut, dataBuffer);
                    }
                }
            }
        }

    }
}
