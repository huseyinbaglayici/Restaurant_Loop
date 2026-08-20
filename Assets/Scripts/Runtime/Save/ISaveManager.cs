namespace RestaurantLoop.Save
{
    public interface ISaveManager
    {
        void Save();
        void Load();
        void SetCurrentLevel(int levelId);
    }
}
