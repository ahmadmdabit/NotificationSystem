using System;
using System.Collections.Generic;

namespace DAL.Entity
{
    public interface IEntity
    {
        object GetKeyValue();
        string GetTableName();
    }
}