/// Public domain code by Christopher Diggins
/// http://www.cat-language.com

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections;
using System.Collections.Generic;

namespace Cat
{
    /// <summary>
    /// The base class for all Cat functions 
    /// </summary>
    public abstract class Function : CatBase
    {
        public Function(string sName, string sType, string sDesc)
        {
            msName = sName;
            msType = sType;
            msDesc = sDesc;
        }

        public Function(string sName, string sType)
        {
            msName = sName;
            msType = sType;
            msDesc = "";
        }

        #region Fields
        public string msName = "_unnamed_"; 
        public string msDesc = "";
        public string msType = "";
        #endregion

        public Function()
        {
        }
        public void SetType(string s)
        {
            msType = s;
        }
        public string GetDesc()
        {
            return msDesc;
        }
        public string GetName()
        {
            return msName;
        }
        public override string ToString()
        {
            return "[" + msName + "]";
        }
        public string GetTypeString()
        {
            return msType;
        }

        public abstract void Eval(Executor exec);

        public virtual Object Invoke()
        {            
            Eval(Executor.Aux);
            return Executor.Aux.GetStack()[0];
        }

        public virtual Object Invoke(Object o)
        {
            Executor.Aux.Push(o);
            Eval(Executor.Aux);
            return Executor.Aux.GetStack()[0];
        }

        public virtual Object Invoke(Object o1, Object o2)
        {
            Executor.Aux.Push(o1);
            Executor.Aux.Push(o2);
            Eval(Executor.Aux);
            return Executor.Aux.GetStack()[0];
        }

        public virtual Object Invoke(Object[] args)
        {
            foreach (Object arg in args)
                Executor.Aux.Push(arg);
            Eval(Executor.Aux);
            return Executor.Aux.GetStack()[0];
        }

        #region static functions
        public static string TypeToString(Type t)
        {
            switch (t.Name)
            {
                case ("HashList"): return "hash_list";
                case ("Int32"): return "int";
                case ("Double"): return "float";
                case ("CatList"): return "list";
                case ("Object"): return "var";
                case ("Function"): return "function";
                case ("Boolean"): return "bool";
                case ("String"): return "string";
                case ("Char"): return "char";
                default: return t.Name;
            }
        }

        public static Type GetReturnType(MethodBase m)
        {
            if (m is ConstructorInfo)
                return (m as ConstructorInfo).DeclaringType;
            if (!(m is MethodInfo))
                throw new Exception("Expected ConstructorInfo or MethodInfo");
            return (m as MethodInfo).ReturnType;
        }

        public static bool HasReturnType(MethodBase m)
        {
            Type t = GetReturnType(m);
            return (t != null) && (!t.Equals(typeof(void)));
        }

        public static bool HasThisType(MethodBase m)
        {
            if (m is ConstructorInfo)
                return false;
            return !m.IsStatic;
        }

        public static Type GetThisType(MethodBase m)
        {
            if (m is ConstructorInfo)
                return null;
            if (!(m is MethodInfo))
                throw new Exception("Expected ConstructorInfo or MethodInfo");
            if (m.IsStatic)
                return null;
            return (m as MethodInfo).DeclaringType;
        }

        public static string MethodToTypeString(MethodBase m)
        {
            string s = "(";

            if (HasThisType(m))
                s += "this=" + TypeToString(m.DeclaringType) + " ";

            foreach (ParameterInfo pi in m.GetParameters())
                s += TypeToString(pi.ParameterType) + " ";

            s += ") -> (";

            if (HasThisType(m))
                s += "this ";

            if (HasReturnType(m))
                s += TypeToString(GetReturnType(m));

            s += ")";

            return s;
        }

        #endregion
    }
  
    /// <summary>
    /// This is a function that pushes an integer onto the stack.
    /// </summary>
    public class IntFunction : Function
    {
        int mnValue;        
        public IntFunction(int x) 
        {
            msName = x.ToString();
            SetType("( -> int)");
            mnValue = x;
        }
        public override void Eval(Executor exec) 
        { 
            exec.Push(GetValue());
        }
        public override string ToString()
        {
            return msName;
        }
        public int GetValue()
        {
            return mnValue;
        }
    }

    /// <summary>
    /// This is a function that pushes an integer onto the stack.
    /// </summary>
    public class FloatFunction : Function
    {
        double mdValue;
        public FloatFunction(double x)
        {
            msName = x.ToString();
            SetType("( -> int)");
            mdValue = x;
        }
        public override void Eval(Executor exec)
        {
            exec.Push(GetValue());
        }
        public double GetValue()
        {
            return mdValue;
        }
    }

    /// <summary>
    /// This is a function that pushes a string onto the stack.
    /// </summary>
    public class StringFunction : Function
    {
        string msValue;
        public StringFunction(string x) 
        {
            msName = "\"" + x + "\"";
            msValue = x;
            SetType("( -> string)");
        }
        public override void Eval(Executor exec)
        {
            exec.Push(GetValue());
        }
        public string GetValue()
        {
            return msValue;
        }
    }

    /// <summary>
    /// This is a function that pushes a string onto the stack.
    /// </summary>
    public class CharFunction : Function
    {
        char mcValue;
        public CharFunction(char x)
        {
            msName = x.ToString();
            mcValue = x;
            SetType("( -> char)");
        }
        public override void Eval(Executor exec)
        {
            exec.Push(GetValue());
        }
        public char GetValue()
        {
            return mcValue;
        }
    }

    /// <summary>
    /// This class represents a dynamically created function, 
    /// e.g. the result of calling the quote function.
    /// </summary>
    public class QuoteValue : Function
    {
        Object mpValue;        
        
        public QuoteValue(Object x) 
        {
            mpValue = x;
            msName = mpValue.ToString();
        }
        public override void Eval(Executor exec)
        {
            exec.Push(mpValue);
        }
        public Object GetValue()
        {
            return mpValue;
        }
    }

    /// <summary>
    /// Represents a quotation (pushes an anonymous function onto a stack)
    /// </summary>
    public class Quotation : Function
    {
        List<Function> mChildren;
        
        public Quotation(List<Function> children)
        {
            mChildren = children;
            msDesc = "pushes an anonymous function onto the stack";
            msType = "( -> ('A -> 'B))";
            msName = "[";
            for (int i = 0; i < mChildren.Count; ++i)
            {
                if (i > 0) msName += " ";
                msName += mChildren[i].GetName();
            }
            msName += "]";
        }

        public override void Eval(Executor exec)
        {
            exec.Push(new QuotedFunction(mChildren));
        }

        public List<Function> GetChildren()
        {
            return mChildren;
        }
    }

    public class QuotedFunction : Function
    {
        List<Function> mChildren;
        
        public QuotedFunction(List<Function> children)
        {
            mChildren = children;
            msDesc = "anonymous function";
            msType = "('A -> 'B)";
            msName = "";
            for (int i = 0; i < mChildren.Count; ++i)
            {
                if (i > 0) msName += " ";
                msName += mChildren[i].GetName();
            }
        }

        public override void Eval(Executor exec)
        {
            foreach (Function f in mChildren)
                f.Eval(exec);
        }

        public List<Function> GetChildren()
        {
            return mChildren;
        }
    }

    /// <summary>
    /// This class represents a function, created by calling
    /// compose.
    /// </summary>
    public class ComposedFunction : Function
    {
        Function mFirst;
        Function mSecond;
        public ComposedFunction(Function first, Function second)
        {
            mFirst = first;
            mSecond = second;
            msName = mFirst.GetName() + " " + mSecond.GetName();
            msDesc = "composed function";
        }
        public override void Eval(Executor exec)
        {
            mFirst.Eval(exec);
            mSecond.Eval(exec);
        }
    }

    /// <summary>
    /// This represents a function call. 
    /// 
    /// For now the only scope is global, but the apporach is that the function call 
    /// is bound to the scope where the call is declared, not where it is called. 
    /// This would matter only if implicit redefines are allowed in the semantics.
    /// </summary>
    public class FunctionName : Function
    {
        public FunctionName(string s)
            : base(s, "???", "")
        {
            msName = s;
        }

        private bool IsBetterMatchThan(Function f, Function g)
        {
            // Methods are always better matches.
            if (f is Method && !(g is Method)) return true;
            if (!(f is Method) && g is Method) return false;

            // A method with more parameters is always a better match
            Method fm = f as Method;
            Method gm = g as Method;

            return fm.GetSignature().IsBetterMatchThan(gm.GetSignature());
        }

        public override void Eval(Executor exec)
        {
            Scope scope = exec.GetGlobalScope();
            if (!scope.FunctionExists(msName))
                throw new Exception(msName + " is not defined");
            List<Function> fs = scope.Lookup(exec.GetStack(), msName);
            if (fs.Count == 0)
                throw new Exception("unable to find " + msName + " with matching types. Types on stack are " 
                    + exec.GetStack().GetTopTypesAsString());
            Function f = fs[0];
            for (int i=1; i < fs.Count; ++i)
            {
                if (IsBetterMatchThan(fs[i], f))
                    f = fs[i];
            }
            f.Eval(exec);
        }
    }

    /// <summary>
    /// Represents a function defined by the user
    /// </summary>
    public class DefinedFunction : Function
    {
        List<Function> mTerms;

        public DefinedFunction(string s, List<Function> terms)
        {
            msName = s;
            msType = "untyped";
            mTerms = terms;
            msDesc = "";
            foreach (Function f in mTerms)
                msDesc += f.GetName() + " ";
        }

        public override void Eval(Executor exec)
        {
            foreach (Function f in mTerms)
                f.Eval(exec);
        }
    }

    /// <summary>
    /// An ObjectBoundMethod is like a delegate, it a method pointer combined with an object pointer
    /// </summary>
    public class ObjectBoundMethod : Function
    {
        MethodInfo mMethod;
        Object mObject;

        public ObjectBoundMethod(Object o, MethodInfo mi)
            : base(mi.Name, MethodToTypeString(mi))
        {
            mMethod = mi;
            mObject = o;
        }

        public override void Eval(Executor exec)
        {
            int n = mMethod.GetParameters().Length;
            Object[] a = new Object[n];
            for (int i = 0; i < n; ++i)
            {
                Object o = exec.Pop();
                a[n - i - 1] = o;
            }
            Object ret = mMethod.Invoke(mObject, a);
            if (!mMethod.ReturnType.Equals(typeof(void)))
                exec.Push(ret);
        }

        public Object GetObject()
        {
            return mObject;
        }
    }
}
