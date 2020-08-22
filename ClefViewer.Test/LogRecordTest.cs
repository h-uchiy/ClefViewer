using NUnit.Framework;

namespace ClefViewer.Test
{
    public class Tests
    {
        private const string rowLine = "{\"@t\":\"2020-08-21T02:58:24.0531399Z\",\"@mt\":\"[51440] ConnectStream#24474730 - Sending headers\\r\\n{\\r\\nAccept: application/json\\r\\nAuthorization: Basic eyJ0eXAiOiJKV1QiLCJhbGciOiJSU0EtT0FFUCIsImVuYyI6IkEyNTZHQ00ifQ.eyJpc3MiOiJKRWRpeFdwZkNsaWVudCIsImF1ZCI6IkpFZGl4QXV0aFNlcnZlciIsInB1YmtleSI6IjxSU0FLZXlWYWx1ZT48TW9kdWx1cz5ub3p0YnJabHBTc1NsRFM2cXU4QWhDc1FneW1kc0N3RWRjRnQreGlvVENRaUowa1FCb1VNSWliNDNXS1NSdk43a3kzRUJKcWlHR1c3djBRVEtjOUwvcVlyUHFUVzZVNG5wUXVJNkZ3bTVBTmFQeHIvQ2Iyalp1S3FKMERoQmt4QXBpNTQ3SC9yK1FadE5YcEthNFR0YVR2ZnFuYURkTkhGeU51aHNieG5wNTA9PC9Nb2R1bHVzPjxFeHBvbmVudD5BUUFCPC9FeHBvbmVudD48L1JTQUtleVZhbHVlPiJ9\\r\\nContent-Type: application/json; charset=utf-8\\r\\nHost: localhost:55432\\r\\nContent-Length: 25\\r\\nExpect: 100-continue\\r\\nConnection: Keep-Alive\\r\\n}.\",\"ActivityId\":\"00000000-0000-0000-0000-000000000000\",\"TraceSource\":\"System.Net\",\"TraceEventType\":\"Information\",\"TraceEventId\":0,\"ThreadId\":7}";
        private const string renderExpected = @"2020-08-21 02:58:24.053 [INF] [51440] ConnectStream#24474730 - Sending headers { Accept: application/json Authorization: Basic eyJ0eXAiOiJKV1QiLCJhbGciOiJSU0EtT0FFUCIsImVuYyI6IkEyNTZHQ00ifQ.eyJpc3MiOiJKRWRpeFdwZkNsaWVudCIsImF1ZCI6IkpFZGl4QXV0aFNlcnZlciIsInB1YmtleSI6IjxSU0FLZXlWYWx1ZT48TW9kdWx1cz5ub3p0YnJabHBTc1NsRFM2cXU4QWhDc1FneW1kc0N3RWRjRnQreGlvVENRaUowa1FCb1VNSWliNDNXS1NSdk43a3kzRUJKcWlHR1c3djBRVEtjOUwvcVlyUHFUVzZVNG5wUXVJNkZ3bTVBTmFQeHIvQ2Iyalp1S3FKMERoQmt4QXBpNTQ3SC9yK1FadE5YcEthNFR0YVR2ZnFuYURkTkhGeU51aHNieG5wNTA9PC9Nb2R1bHVzPjxFeHBvbmVudD5BUUFCPC9FeHBvbmVudD48L1JTQUtleVZhbHVlPiJ9 Content-Type: application/json; charset=utf-8 Host: localhost:55432 Content-Length: 25 Expect: 100-continue Connection: Keep-Alive }.";

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestRender1()
        {
            var logRecord = new LogRecord(rowLine, true);
            Assert.That(logRecord.DisplayText, Is.EqualTo(renderExpected));
            logRecord.Render = false;
            Assert.That(logRecord.DisplayText, Is.EqualTo(rowLine));
        }
        
        [Test]
        public void TestRender2()
        {
            var logRecord = new LogRecord(rowLine, false);
            Assert.That(logRecord.DisplayText, Is.EqualTo(rowLine));
            logRecord.Render = true;
            Assert.That(logRecord.DisplayText, Is.EqualTo(renderExpected));
        }
    }
}