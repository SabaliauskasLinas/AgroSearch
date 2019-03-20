using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AgroAdd.Services
{
    

    public class LoggingService
    {
        private readonly string _eventLogSource = "AgroAdds";
        private readonly string _logPath;

        public LoggingService()
        {
            try
            {
                _logPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Logs");

                if (!Directory.Exists(_logPath))
                    Directory.CreateDirectory(_logPath);
            }
            catch(Exception ex)
            {
                LogEvent(new Exception("Unahndled exception while constructing LoggingService", ex));
                throw;
            }
            
        }

        public void LogText(string text)
        {
            try
            {
                if (text.Last().ToString() != Environment.NewLine)
                    text += Environment.NewLine;
                var file = GetLogPath();
                var date = DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss]: ");
                File.AppendAllText(file, date + text);
            }
            catch (Exception ex)
            {
                LogEvent(ex);
            }

        }
        public void LogException(Exception exception, string text)
        {
            try
            {
                var file = GetLogPath();
                var date = DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss]: ");
                File.AppendAllText(file, date + text + Environment.NewLine + ParseException(exception).ToString());
            }
            catch (Exception ex)
            {
                LogEvent(ex);
            }

        }
        public void LogEvent(Exception ex)
        {
            try
            {
                var appLog = new EventLog();
                appLog.Source = _eventLogSource;
                appLog.WriteEntry(ParseException(ex).ToString(), EventLogEntryType.Error);
            }
            catch (Exception)
            {

            }

        }

        private StringBuilder ParseException(Exception ex, StringBuilder existingBuilder = null)
        {
            if (existingBuilder == null)
                existingBuilder = new StringBuilder();
            existingBuilder.AppendLine(ex.Message);
            existingBuilder.AppendLine(ex.StackTrace);
            if (ex.InnerException != null)
                ParseException(ex.InnerException, existingBuilder);
            return existingBuilder;
        }

        private string GetLogPath()
        {
            return Path.Combine(_logPath, DateTime.Now.ToString("yyyy-MM") + "Log.txt");
        }

    }
}
