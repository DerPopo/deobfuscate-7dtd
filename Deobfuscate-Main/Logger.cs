using System;
using System.IO;

namespace DeobfuscateMain
{
	public class Logger
	{
		private bool logToConsole;
		private FileStream fileLogger;
		private StreamWriter fileWriter;
		private Logger parentLogger;
		public Logger(string logPath, Logger parentLogger, bool logToConsole)
		{
			this.logToConsole = logToConsole;
			fileLogger = null;
			fileWriter = null;
			this.parentLogger = parentLogger;
			if (logPath != null) {
				try {
					File.Delete(logPath);
					fileLogger = File.OpenWrite(logPath);
					fileWriter = new StreamWriter(fileLogger);
				} catch (Exception) {
					fileLogger = null;
					Console.WriteLine ("WARNING : Unable to open file '" + logPath + "' for writing!");
				}
			}
		}

		public void Log(string str)
		{
			if (logToConsole) {
				LogConsole (str);
			}
			try {
				LogFile(str);
			} catch (Exception e) {
				Console.WriteLine ("WARNING : An exception occured while writing to the log file :");
				Console.WriteLine(e.ToString());
			}
			if (parentLogger != null) {
				parentLogger.Log (str);
			}
		}
		public bool LogFile(string str)
		{
			if (fileWriter != null) {
				fileWriter.Write(str + "\r\n");
				fileWriter.Flush();
			}
			return fileWriter != null;
		}
		public void LogConsole(string str)
		{
			Console.WriteLine(str);
		}

		public void Close()
		{
			if (fileWriter != null)
				fileWriter.Close();
		}
	}
}

