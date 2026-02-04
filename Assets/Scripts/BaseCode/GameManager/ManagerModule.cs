namespace gameManagerModule
{
    public interface IModule
    {
        public void AwakeModule();
        public void OnGameStart();
    }

    public interface IConfigurableModule: IModule
    {
        public void SaveConfig();
        public void LoadConfig();
        public string GetConfigJson();
    }

    public interface ILateUpdateModule : IModule
    {
        public void LateUpdateModule();
    }

    public interface IFixedUpdateModule : IModule
    {
        public void FixedUpdateModule();
    }

    public interface IUpdateModule : IModule
    {
        public void UpdateModule();
    }
}