namespace RestaurantLoop.Runtime.Save
{
    public interface ISaveManager
    {
        void Save();
        void Load();
        void SetCurrentLevel(int levelId);
    }
}
