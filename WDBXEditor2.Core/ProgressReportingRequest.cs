using MediatR;

namespace WDBXEditor2.Core
{
    public abstract class ProgressReportingRequest: IRequest
    {
        public IProgressReporter? ProgressReporter { get; set; } = null; 
    }
}
