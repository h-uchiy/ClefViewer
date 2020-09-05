using System;
using System.IO;
using DevExpress.Mvvm;
using DevExpress.Mvvm.UI.Native;
using Newtonsoft.Json.Linq;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact.Reader;
using Serilog.Formatting.Display;

namespace ClefViewer
{
    public class LogRecord : ViewModelBase
    {
        private const string TimeStampFormat = "yyyy-MM-dd HH:mm:ss.fff";
        private static readonly ITextFormatter _levelFormatter = new MessageTemplateTextFormatter("{Level:u3}");
        private static readonly ITextFormatter _messageFormatter = new MessageTemplateTextFormatter("{Message:l}");
        private readonly MainWindowViewModel _outer;
        private LogEvent _logEvent;

        public LogRecord(MainWindowViewModel outer, string rowText, int lineNumber)
        {
            outer.WeakPropertyChanged += (sender, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(MainWindowViewModel.Render):
                        RaisePropertiesChanged(nameof(DisplayText));
                        break;
                    case nameof(MainWindowViewModel.ShowUTC):
                        RaisePropertiesChanged(nameof(Timestamp));
                        break;
                }
            };
            _outer = outer;
            LineNumber = lineNumber;
            RowText = rowText.Trim();
        }

        public int LineNumber { get; }

        public string Timestamp => (ShowUTC ? LogEvent.Timestamp.UtcDateTime : LogEvent.Timestamp.LocalDateTime).ToString(TimeStampFormat);

        public string DisplayLevel => RenderMessage(_levelFormatter);

        public string RowText { get; }

        public string DisplayText => (Render ? RenderMessage(_messageFormatter) : RowText).Replace(Environment.NewLine, " ");

        public LogEvent LogEvent => _logEvent ??= LogEventReader.ReadFromJObject(JObject.Parse(RowText));

        private bool Render => _outer.Render;

        private bool ShowUTC => _outer.ShowUTC;

        public static bool operator ==(LogRecord left, LogRecord right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LogRecord left, LogRecord right)
        {
            return !Equals(left, right);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((LogRecord)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (LineNumber.GetHashCode() * 397) ^ (RowText != null ? RowText.GetHashCode() : 0);
            }
        }

        protected bool Equals(LogRecord other)
        {
            return LineNumber == other.LineNumber
                   && RowText == other.RowText;
        }

        private string RenderMessage(ITextFormatter formatter)
        {
            var stringWriter = new StringWriter();
            formatter.Format(LogEvent, stringWriter);
            return stringWriter.ToString().Trim();
        }
    }
}