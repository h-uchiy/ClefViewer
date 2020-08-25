using System;
using System.IO;
using ClefViewer.Properties;
using DevExpress.Mvvm;
using Newtonsoft.Json.Linq;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact.Reader;
using Serilog.Formatting.Display;

namespace ClefViewer
{
    public class LogRecord : ViewModelBase
    {
        private const string _timeStampFormat = "yyyy-MM-dd HH:mm:ss.fff";
        private static readonly ITextFormatter _levelFormatter = new MessageTemplateTextFormatter("{Level:u3}");
        private static readonly ITextFormatter _messageFormatter = new MessageTemplateTextFormatter("{Message:l}");
        private readonly MainWindowViewModel _outer;
        private LogEvent _logEvent;

        public LogRecord(MainWindowViewModel outer, string rowText, int lineNumber)
        {
            outer.PropertyChanged += (sender, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(Render):
                        RaisePropertiesChanged(nameof(Render));
                        RaisePropertiesChanged(nameof(DisplayText));
                        break;
                    case nameof(UTC):
                        RaisePropertiesChanged(nameof(UTC));
                        RaisePropertiesChanged(nameof(Timestamp));
                        break;
                }
            };
            _outer = outer;
            LineNumber = lineNumber;
            RowText = rowText.Trim();
        }

        public int LineNumber { get; }

        public string Timestamp => (UTC ? LogEvent.Timestamp.UtcDateTime : LogEvent.Timestamp.LocalDateTime).ToString(_timeStampFormat);

        public string ErrorLevel => RenderMessage(_levelFormatter);

        public string RowText { get; }

        public string DisplayText => (Render ? RenderMessage(_messageFormatter) : RowText).Replace(Environment.NewLine, " ");

        public bool Render => _outer.Render;

        public bool UTC => _outer.UTC;

        public double LineNumberWidth
        {
            get => Settings.Default.NumberWidth;
            set => Settings.Default.NumberWidth = value;
        }

        public double TimestampWidth
        {
            get => Settings.Default.TimestampWidth;
            set => Settings.Default.TimestampWidth = value;
        }

        public double ErrorLevelWidth
        {
            get => Settings.Default.ErrorLevelWidth;
            set => Settings.Default.ErrorLevelWidth = value;
        }

        private LogEvent LogEvent
        {
            get
            {
                if (_logEvent != null)
                {
                    return _logEvent;
                }

                var jObject = JObject.Parse(RowText);
                _logEvent = LogEventReader.ReadFromJObject(jObject);
                return _logEvent;
            }
        }

        private string RenderMessage(ITextFormatter formatter)
        {
            var stringWriter = new StringWriter();
            formatter.Format(LogEvent, stringWriter);
            return stringWriter.ToString().Trim();
        }
    }
}