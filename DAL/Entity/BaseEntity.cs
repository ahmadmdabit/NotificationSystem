using Common.Extensions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace DAL.Entity
{
    public abstract class BaseEntity : IEntity
    {
        public object GetKeyValue()
        {
            return this.PropertyFindValueByAttribute(typeof(KeyAttribute));
        }

        public string GetTableName()
        {
            return ((TableAttribute)this.GetType().GetCustomAttributes(typeof(TableAttribute), false).FirstOrDefault()).Name;
        }
    }
}