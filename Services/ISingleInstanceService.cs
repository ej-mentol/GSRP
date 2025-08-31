namespace GSRP.Services
{
    public interface ISingleInstanceService
    {
        bool IsFirstInstance();
        void ReleaseInstance();
    }
}