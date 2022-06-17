using System;
namespace PFC_Bot.Modules
{
    public class DatabaseManager
    {
        ApplicationDbContext database;
        private DatabaseManager()
        {

        }

        public ApplicationDbContext getDatabase()
        {
            return database;
        }

    }
}
