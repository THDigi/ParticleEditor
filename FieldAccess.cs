using System;
using System.Reflection;
using System.Reflection.Emit;

/// <summary>
/// From Medieval Engineers' decompiled code, offers box-less field getters.
/// </summary>
public static class FieldAccess
{
    public static Func<TType, TMember> CreateGetter<TType, TMember>(this FieldInfo field)
    {
        string name = field.ReflectedType.FullName + ".get_" + field.Name;
        DynamicMethod dynamicMethod = new DynamicMethod(name, typeof(TMember), new Type[1]
        {
            typeof(TType)
        }, restrictedSkipVisibility: true);
        ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
        if(field.IsStatic)
        {
            iLGenerator.Emit(OpCodes.Ldsfld, field);
        }
        else
        {
            iLGenerator.Emit(OpCodes.Ldarg_0);
            if(field.DeclaringType != typeof(TType))
            {
                iLGenerator.Emit(OpCodes.Castclass, field.DeclaringType);
            }
            iLGenerator.Emit(OpCodes.Ldfld, field);
        }
        iLGenerator.Emit(OpCodes.Ret);
        return (Func<TType, TMember>)dynamicMethod.CreateDelegate(typeof(Func<TType, TMember>));
    }

    public static Action<TType, TMember> CreateSetter<TType, TMember>(this FieldInfo field)
    {
        string name = field.ReflectedType.FullName + ".set_" + field.Name;
        DynamicMethod dynamicMethod = new DynamicMethod(name, null, new Type[2]
        {
            typeof(TType),
            typeof(TMember)
        }, restrictedSkipVisibility: true);
        ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
        if(field.IsStatic)
        {
            iLGenerator.Emit(OpCodes.Ldarg_1);
            iLGenerator.Emit(OpCodes.Stsfld, field);
        }
        else
        {
            if(typeof(TType).IsValueType)
            {
                iLGenerator.Emit(OpCodes.Ldarga, 0);
            }
            else
            {
                iLGenerator.Emit(OpCodes.Ldarg_0);
            }
            if(field.DeclaringType != typeof(TType))
            {
                iLGenerator.Emit(OpCodes.Castclass, field.DeclaringType);
            }
            iLGenerator.Emit(OpCodes.Ldarg_1);
            iLGenerator.Emit(OpCodes.Stfld, field);
        }
        iLGenerator.Emit(OpCodes.Ret);
        return (Action<TType, TMember>)dynamicMethod.CreateDelegate(typeof(Action<TType, TMember>));
    }

    public static Getter<TType, TMember> CreateGetterRef<TType, TMember>(this FieldInfo field)
    {
        string name = field.ReflectedType.FullName + ".get_" + field.Name;
        DynamicMethod dynamicMethod = new DynamicMethod(name, null, new Type[2]
        {
            typeof(TType).MakeByRefType(),
            typeof(TMember).MakeByRefType()
        }, restrictedSkipVisibility: true);
        ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
        if(field.IsStatic)
        {
            throw new NotImplementedException();
        }
        iLGenerator.Emit(OpCodes.Ldarg_1);
        iLGenerator.Emit(OpCodes.Ldarg_0);
        if(!typeof(TType).IsValueType)
        {
            iLGenerator.Emit(OpCodes.Ldind_Ref);
        }
        if(field.DeclaringType != typeof(TType))
        {
            iLGenerator.Emit(OpCodes.Castclass, field.DeclaringType);
        }
        iLGenerator.Emit(OpCodes.Ldfld, field);
        if(field.FieldType != typeof(TMember))
        {
            iLGenerator.Emit(OpCodes.Castclass, typeof(TMember));
        }
        if(!typeof(TMember).IsValueType)
        {
            iLGenerator.Emit(OpCodes.Stind_Ref);
        }
        else
        {
            iLGenerator.Emit(OpCodes.Stobj, typeof(TMember));
        }
        iLGenerator.Emit(OpCodes.Ret);
        return (Getter<TType, TMember>)dynamicMethod.CreateDelegate(typeof(Getter<TType, TMember>));
    }

    public static Setter<TType, TMember> CreateSetterRef<TType, TMember>(this FieldInfo field)
    {
        string name = field.ReflectedType.FullName + ".set_" + field.Name;
        DynamicMethod dynamicMethod = new DynamicMethod(name, null, new Type[2]
        {
            typeof(TType).MakeByRefType(),
            typeof(TMember).MakeByRefType()
        }, restrictedSkipVisibility: true);
        ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
        if(field.IsStatic)
        {
            iLGenerator.Emit(OpCodes.Ldarg_1);
            iLGenerator.Emit(OpCodes.Stsfld, field);
        }
        else
        {
            iLGenerator.Emit(OpCodes.Ldarg_0);
            if(!typeof(TType).IsValueType)
            {
                iLGenerator.Emit(OpCodes.Ldind_Ref);
            }
            if(field.DeclaringType != typeof(TType))
            {
                iLGenerator.Emit(OpCodes.Castclass, field.DeclaringType);
            }
            iLGenerator.Emit(OpCodes.Ldarg_1);
            if(!typeof(TMember).IsValueType)
            {
                iLGenerator.Emit(OpCodes.Ldind_Ref);
            }
            else
            {
                iLGenerator.Emit(OpCodes.Ldobj, typeof(TMember));
            }
            if(field.FieldType != typeof(TMember))
            {
                iLGenerator.Emit(OpCodes.Castclass, field.FieldType);
            }
            iLGenerator.Emit(OpCodes.Stfld, field);
        }
        iLGenerator.Emit(OpCodes.Ret);
        return (Setter<TType, TMember>)dynamicMethod.CreateDelegate(typeof(Setter<TType, TMember>));
    }

    public static Action<TMember> CreateSetter<TMember>(this FieldInfo field)
    {
        if(!field.IsStatic)
        {
            throw new InvalidOperationException("Field must be static");
        }
        string name = field.ReflectedType.FullName + ".set_" + field.Name;
        DynamicMethod dynamicMethod = new DynamicMethod(name, null, new Type[1]
        {
            typeof(TMember)
        }, restrictedSkipVisibility: true);
        ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
        iLGenerator.Emit(OpCodes.Ldarg_0);
        iLGenerator.Emit(OpCodes.Stsfld, field);
        iLGenerator.Emit(OpCodes.Ret);
        return (Action<TMember>)dynamicMethod.CreateDelegate(typeof(Action<TMember>));
    }
}
