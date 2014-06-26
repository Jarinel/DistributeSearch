using System;
using System.IO;
using System.Text;

namespace DistributeSearchProject
{
    class Log {
        private static object sync = new object();

        internal enum LogEnum {
            Warning,
            Error,
            Info
        }

        public static void WriteInfo(string info) {
            Write(LogEnum.Info, info);
        }

        public static void WriteWarning(string warning) {
            Write(LogEnum.Warning, warning);
        }

        public static void WriteError(string error) {
            Write(LogEnum.Error, error);
        }

        public static void WriteException(Exception e) {
            Write(LogEnum.Error, "", e);
        }

        public static void Write(LogEnum logEnum, string error = "", Exception exception = null) {
            var write = false;
            switch (logEnum) {
                case LogEnum.Warning:
                    write = Settings.LOG_WARNING;
                    break;
                    case LogEnum.Info:
                    write = Settings.LOG_INFO; break;
                case LogEnum.Error:
                    write = Settings.LOG_ERROR; break;
                default:
                    write = true;
                    break;
            } try {
                var pathToLog = Settings.LOG_PATH;
                if (!Directory.Exists(pathToLog))
                    Directory.CreateDirectory(pathToLog); // Создаем директорию, если нужно

                var filename = Path.Combine(pathToLog, string.Format("{0}.log", AppDomain.CurrentDomain.FriendlyName));
                string fullText;
                if (exception != null)
                    fullText = string.Format("[{0:dd.MM.yyy HH:mm:ss.fff}] [{1}.{2}()] {3} : {4}\r\n",
                         DateTime.Now, exception.TargetSite.DeclaringType, exception.TargetSite.Name, error, exception.Message);
                else
                    fullText = string.Format("[{0:dd.MM.yyy HH:mm:ss.fff}] |{1}\t|:{2,5}\r\n",
                        DateTime.Now, logEnum, error);
                if (write || exception != null)
                    lock (sync) {
                        File.AppendAllText(filename, fullText, Encoding.UTF8);
//                        File.AppendAllText(filename, fullText, Encoding.GetEncoding("Windows-1251"));
                    }
            }
            catch
            {
                // Перехватываем все и ничего не делаем
            }
        }
    }
}
