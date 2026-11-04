using System;
using System.Collections.Generic;
using System.Reflection;

namespace Framework
{
    /// <summary>
    /// 이 클래스가 담당하는 "종류" 키 (보통 데이터 enum 값). 한 클래스가 여러 키를 맡을 수 있다.
    /// 예: <c>[Kind(EAbilityType.Dash)] public class DashAbility : GameAbility</c>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class KindAttribute : Attribute
    {
        public readonly int Key;

        public KindAttribute(object InKey)
        {
            Key = Convert.ToInt32(InKey);
        }
    }

    /// <summary>
    /// 종류 키(enum) → 다형성 객체 (Strategy / Factory 레지스트리, 2026-11-01).
    /// enum 은 CSV · 직렬화 데이터의 키로만 남고, 그 키가 할 일은 [Kind] 를 단 TBase 하위 클래스가 virtual 메서드로 구현한다.
    /// switch 로 분기하던 곳은 <see cref="Get"/>(상태 없는 공유 객체) 또는 <see cref="Create"/>(새 인스턴스) 한 줄이 된다.
    /// 새 종류 = 클래스 하나 추가 (switch 를 고치지 않는다 — 개방/폐쇄 원칙).
    /// 하위 클래스는 처음 쓸 때 TBase 의 어셈블리와 그 어셈블리를 참조하는 어셈블리에서 한 번 찾는다 (매개변수 없는 생성자 필요).
    /// </summary>
    public static class KindRegistry<TBase> where TBase : class
    {
        private static Dictionary<int, Type> Types;
        private static readonly Dictionary<int, TBase> Shared = new Dictionary<int, TBase>();

        /// <summary>키에 해당하는 공유 인스턴스 (상태 없는 전략용). 없으면 null.</summary>
        public static TBase Get(int InKey)
        {
            TBase instance;
            if (Shared.TryGetValue(InKey, out instance) == true)
            {
                return instance;
            }

            instance = Create(InKey);
            if (instance != null)
            {
                Shared.Add(InKey, instance);
            }

            return instance;
        }

        public static TBase Get<TEnum>(TEnum InKey) where TEnum : struct, Enum
        {
            return Get(Convert.ToInt32(InKey));
        }

        /// <summary>키에 해당하는 새 인스턴스 (상태를 가지는 객체용). 없으면 null.</summary>
        public static TBase Create(int InKey)
        {
            ensureTypes();
            Type type;
            return Types.TryGetValue(InKey, out type) == true ? (TBase)Activator.CreateInstance(type) : null;
        }

        public static TBase Create<TEnum>(TEnum InKey) where TEnum : struct, Enum
        {
            return Create(Convert.ToInt32(InKey));
        }

        /// <summary>등록된 키 수 (테스트 · 검증용).</summary>
        public static int Count
        {
            get
            {
                ensureTypes();
                return Types.Count;
            }
        }

        public static bool Has(int InKey)
        {
            ensureTypes();
            return Types.ContainsKey(InKey);
        }

        private static void ensureTypes()
        {
            if (Types != null)
            {
                return;
            }

            Types = new Dictionary<int, Type>();
            var baseType = typeof(TBase);
            string baseAssembly = baseType.Assembly.GetName().Name;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly != baseType.Assembly && references(assembly, baseAssembly) == false)
                {
                    continue;
                }

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract == true || baseType.IsAssignableFrom(type) == false)
                    {
                        continue;
                    }

                    foreach (KindAttribute kind in type.GetCustomAttributes(typeof(KindAttribute), false))
                    {
                        if (Types.ContainsKey(kind.Key) == true)
                        {
                            Log.Error($"KindRegistry<{baseType.Name}>: key {kind.Key} registered twice ({Types[kind.Key].Name}, {type.Name})");
                            continue;
                        }

                        Types.Add(kind.Key, type);
                    }
                }
            }
        }

        private static bool references(Assembly InAssembly, string InName)
        {
            foreach (var reference in InAssembly.GetReferencedAssemblies())
            {
                if (reference.Name == InName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
