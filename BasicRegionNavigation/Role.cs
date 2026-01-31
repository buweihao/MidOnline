using Core;

using System;
using System.Reflection;

namespace BasicRegionNavigation
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
                throw new UnauthorizedAccessException("权限不足");
            }
        }

        public void OnExit() { }

        public void OnException(Exception exception) { }
    }
}
