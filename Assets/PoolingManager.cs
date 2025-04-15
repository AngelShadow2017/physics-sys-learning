using System.Collections.Generic;

namespace Core.Algorithm
{
    public interface IPoolable
    {
        public void Reset();
    }
  
    public static class ObjectPool<T> where T : class, IPoolable, new()
    {
        private static readonly Stack<T> _pool = new Stack<T>();
        private static readonly object _lock = new object();

        // 创建并初始化对象  
        private static T CreateInstance()
        {
            var instance = new T();
            instance.Reset();
            return instance;
        }

        // 静态泛型方法获取对象  
        public static T GetObject()
        {
            if (_pool.Count > 0)
            {
                var instance = _pool.Pop();
                instance.Reset();
                return instance;
            }

            // 如果没有可用对象，则创建一个新的实例  
            return CreateInstance();
        }

        // 静态方法回收对象  
        public static void ReturnObject(T obj)
        {
            if (obj == null)
                return;

            _pool.Push(obj);
        }
    }
    
}
