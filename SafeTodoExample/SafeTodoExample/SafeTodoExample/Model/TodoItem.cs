using System;

namespace SafeTodoExample.Model
{
    public class TodoItem
    {
        public string Title { get; set; }
        public string Detail { get; set; }
        public int ItemId => new Random().Next(0, 999999);
    }
}
