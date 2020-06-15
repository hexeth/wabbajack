﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        public async Task IngestMetric(Metric metric)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync(@"INSERT INTO dbo.Metrics (Timestamp, Action, Subject, MetricsKey) VALUES (@Timestamp, @Action, @Subject, @MetricsKey)", metric);
        }
        
        public async Task IngestAccess(string ip, string log)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync(@"INSERT INTO dbo.AccessLog (Timestamp, Action, Ip) VALUES (@Timestamp, @Action, @Ip)", new
            {
                Timestamp = DateTime.UtcNow,
                Ip = ip,
                Action = log
            });
        }
        
        public async Task<IEnumerable<AggregateMetric>> MetricsReport(string action)
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<AggregateMetric>(@"
                        SELECT d.Date, d.GroupingSubject as Subject, Count(*) as Count FROM 
                        (select DISTINCT CONVERT(date, Timestamp) as Date, GroupingSubject, Action, MetricsKey from dbo.Metrics) m
                        RIGHT OUTER JOIN
                        (SELECT CONVERT(date, DATEADD(DAY, number + 1, dbo.MinMetricDate())) as Date, GroupingSubject, Action
                        FROM master..spt_values
                        CROSS JOIN (
                          SELECT DISTINCT GroupingSubject, Action FROM dbo.Metrics 
                          WHERE MetricsKey is not null 
                          AND Subject != 'Default'
                          AND TRY_CONVERT(uniqueidentifier, Subject) is null) as keys
                        WHERE type = 'P'
                        AND DATEADD(DAY, number+1, dbo.MinMetricDate()) <= dbo.MaxMetricDate()) as d
                        ON m.Date = d.Date AND m.GroupingSubject = d.GroupingSubject AND m.Action = d.Action
                        WHERE d.Action = @action
                        AND d.Date >= DATEADD(month, -1, GETUTCDATE())
                        group by d.Date, d.GroupingSubject, d.Action
                        ORDER BY d.Date, d.GroupingSubject, d.Action", new {Action = action}))
                .ToList();
        }

        public async Task<List<(DateTime, string, string)>> FullTarReport(string key)
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<(DateTime, string, string)>(@"
                                SELECT u.Timestamp, u.Path, u.MetricsKey FROM
                                (SELECT al.Timestamp, JSON_VALUE(al.Action, '$.Path') as Path, al.MetricsKey FROM dbo.AccessLog al
                                WHERE al.MetricsKey in (SELECT DISTINCT MetricsKey from AccessLog al WHERE al.Ip in (SELECT DISTINCT Ip from AccessLog where MetricsKey = @MetricsKey))
                                UNION ALL
                                SELECT m.Timestamp, m.Action + ' ' + m.Subject as Path, m.MetricsKey FROM dbo.Metrics m
                                WHERE m.MetricsKey in (SELECT DISTINCT MetricsKey from AccessLog al WHERE al.Ip in (SELECT DISTINCT Ip from AccessLog where MetricsKey = @MetricsKey)
                                AND m.Action != 'TarKey')) u
                                ORDER BY u.Timestamp Desc",
                new {MetricsKey = key})).ToList();

        }
    }
}
