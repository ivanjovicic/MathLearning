using System;
using System.Collections.Generic;
using System.Text;

namespace MathLearning.Domain.Entities
{
    public class Category
    {
        public int Id { get; private set; }
        public string Name { get; private set; } = "";

        private Category() { }

        public Category(string name)
        {
            Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required") : name;
        }

        public void Rename(string name)
        {
            Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required") : name;
        }
    }
}
