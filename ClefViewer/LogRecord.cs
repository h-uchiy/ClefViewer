using System;
using System.IO;
using ClefViewer.Properties;
using DevExpress.Mvvm;
using Newtonsoft.Json.Linq;
using Serilog.Events;
using Serilog.Formatting.Compact.Reader;
using Serilog.Formatting.Display;

namespace ClefViewer
{
    public class LogRecord : ViewModelBase
    {
        private LogEvent _logEvent;
        private bool _render;

        public LogRecord(string rowText, bool render)
        {
            RowText = rowText.Trim();
            Render = render;
        }

        public string RowText { get; }

        public string DisplayText => (Render ? RenderMessage() : RowText).Replace(Environment.NewLine, " ");

        public bool Render
        {
            get => _render;
            set => SetValue(ref _render, value, () => RaisePropertiesChanged(nameof(DisplayText)));
        }

        private LogEvent GetLogEvent()
        {
            if (_logEvent != null)
            {
                return _logEvent;
            }

            var jObject = JObject.Parse(RowText);
            _logEvent = LogEventReader.ReadFromJObject(jObject);
            return _logEvent;
        }

        private string RenderMessage()
        {
            var formatter = new MessageTemplateTextFormatter(Settings.Default.RenderTemplate);
            var stringWriter = new StringWriter();
            formatter.Format(GetLogEvent(), stringWriter);
            return stringWriter.ToString().Trim();
        }
    }
}