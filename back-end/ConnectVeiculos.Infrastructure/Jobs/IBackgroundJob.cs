namespace ConnectVeiculos.Infrastructure.Jobs
{
    public interface IBackgroundJob
    {
        Task ExecuteAsync();
        string JobName { get; }
        string CronExpression { get; }
    }
}
