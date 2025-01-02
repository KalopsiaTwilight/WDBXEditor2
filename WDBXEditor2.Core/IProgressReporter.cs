namespace WDBXEditor2.Core
{
    public interface IProgressReporter
    {
        void ReportProgress(int progressPercentage);
        void SetOperationName(string operationName);
        void SetIsIndeterminate(bool isIndeterminate);
    }
}
