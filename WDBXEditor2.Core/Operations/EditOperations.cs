using DBCD;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WDBXEditor2.Core.Operations
{
    public class BaseEditOperationRequest: ProgressReportingRequest
    {
        public IDBCDStorage? Storage { get; set; }
    }

    public class SetColumnOperation : BaseEditOperationRequest
    {
        public string ColumnName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class ReplaceColumnOperation: BaseEditOperationRequest
    {
        public string ColumnName { get; set; } = string.Empty;
        public string SearchValue { get; set; } = string.Empty;
        public string ReplacementValue { get; set; } = string.Empty;
    }

    public class SetDependentColumnOperation: BaseEditOperationRequest
    {
        public string PrimaryColumnName { get; set; } = string.Empty;
        public string PrimaryValue { get; set; } = string.Empty;
        public string ForeignColumnName { get; set; } = string.Empty;
        public string ForeignValue { get; set; } = string.Empty;
    }

    public class SetFlagOperation : BaseEditOperationRequest
    {
        public string ColumnName { get; set; } = string.Empty;
        public bool Unset { get; set; }
        public uint BitValue { get; set; }
    }


    public class EditOperationsRequestHandler : IRequestHandler<ReplaceColumnOperation>, IRequestHandler<SetColumnOperation>,
        IRequestHandler<SetFlagOperation>, IRequestHandler<SetDependentColumnOperation>
    {
        public Task Handle(ReplaceColumnOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for edit operation.");

            request.ProgressReporter?.SetOperationName("Edit - Replacing column values...");

            var processedCount = 0;
            var totalCount = dbcdStorage.Values.Count;

            foreach (var row in dbcdStorage.Values)
            {
                if (row[request.ColumnName].ToString() == request.SearchValue)
                {
                    DBCDHelper.SetDBCRowColumn(row, request.ColumnName, request.ReplacementValue);
                }

                var progress = (int)((float)processedCount++ / totalCount * 100f);
                request.ProgressReporter?.ReportProgress(progress);
            }

            return Task.CompletedTask;
        }

        public Task Handle(SetColumnOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for edit operation.");

            request.ProgressReporter?.SetOperationName("Edit - Setting column values...");

            var processedCount = 0;
            var totalCount = dbcdStorage.Values.Count;

            foreach (var row in dbcdStorage.Values)
            {
                DBCDHelper.SetDBCRowColumn(row, request.ColumnName, request.Value);
                var progress = (int)((float)processedCount++ / totalCount * 100f);
                request.ProgressReporter?.ReportProgress(progress);
            }

            return Task.CompletedTask;
        }

        public Task Handle(SetDependentColumnOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for edit operation.");

            request.ProgressReporter?.SetOperationName("Edit - Setting dependent column values...");

            var processedCount = 0;
            var totalCount = dbcdStorage.Values.Count;

            foreach (var row in dbcdStorage.Values)
            {
                if (row[request.PrimaryColumnName].ToString() == request.PrimaryValue)
                {
                    DBCDHelper.SetDBCRowColumn(row, request.ForeignColumnName, request.ForeignValue);
                }
                var progress = (int)((float)processedCount++ / totalCount * 100f);
                request.ProgressReporter?.ReportProgress(progress);
            }

            return Task.CompletedTask;
        }

        public Task Handle(SetFlagOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for edit operation.");

            request.ProgressReporter?.SetOperationName("Edit - Setting bit values...");

            var processedCount = 0;
            var totalCount = dbcdStorage.Values.Count;

            foreach (var row in dbcdStorage.Values)
            {
                var rowVal = Convert.ToInt32(row[request.ColumnName]);
                if (request.Unset)
                {
                    if ((rowVal & request.BitValue) > 0)
                    {
                        DBCDHelper.SetDBCRowColumn(row, request.ColumnName, rowVal - request.BitValue);
                    }
                }
                else
                {
                    if ((rowVal & request.BitValue) == 0)
                    {
                        DBCDHelper.SetDBCRowColumn(row, request.ColumnName, rowVal + request.BitValue);
                    }

                }
                var progress = (int)((float)processedCount++ / totalCount * 100f);
                request.ProgressReporter?.ReportProgress(progress);
            }

            return Task.CompletedTask;
        }
    }
}
