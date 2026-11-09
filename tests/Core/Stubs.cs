namespace StardewModdingAPI { public enum LogLevel { Debug, Error } public interface IMonitor { void Log(string text, LogLevel level); } }
