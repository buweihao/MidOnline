
using System;
using System.Reflection;

namespace Core
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RequireRoleAttribute : Attribute
    {
        private Role _requiredRole;
        private MethodBase _method;


        public RequireRoleAttribute(Role requiredRole)
        {
            _requiredRole = requiredRole;
        }

        public void Init(object instance, MethodBase method, object[] args)
        {
            _method = method;
        }

        public void OnEntry()
        {
            if (CurrentUserContext.role < _requiredRole)
            {
                var msg = $"❌ 权限不足：{CurrentUserContext.role} 无法调用 {_method.Name}，需要权限：{_requiredRole}";
                Console.WriteLine(msg);
                throw new UnauthorizedAccessException(msg);
            }
            Console.WriteLine($"✅ 权限通过：{CurrentUserContext.role} 执行 {_method.Name}");
        }

        public void OnExit() { }

        public void OnException(Exception exception) { }
    }
    public enum Role
    {
        Guest = 0,
        User = 1,
        Manager = 2,
        Admin = 3,
        SuperAdmin = 4
    }
}
