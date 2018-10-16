using System;

namespace SafetodoExample.Tests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TestPriorityAttribute : Attribute
    {
        public int Priority { get; set; }
        public TestPriorityAttribute(int Priority)
        {
            this.Priority = Priority;
        }
    }
}
