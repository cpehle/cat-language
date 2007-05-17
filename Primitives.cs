/// Public domain code by Christopher Diggins
/// http://www.cat-language.com

using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Cat
{
    public class CatException : Exception
    {
        object data;

        public CatException(object o)
        {
            data = o;
        }

        public object GetObject()
        {
            return data;
        }
    }

    /// <summary>
    /// This is used to as a base class for the variou dynamic function dispatch functions
    /// It can not be placed with in the Primitives class, due to some design flaw.
    /// </summary>
    public class Dispatch : PrimitiveFunction
    {
        public Dispatch(string sName, string sType)
            : base(sName, sType, "dispatches a function based on the type of the top stack item")
        { }

        public override void Eval(Executor exec)
        {
            FList list = exec.TypedPop<FList>();
            Type t = exec.Peek().GetType();
            FList iter = list.GetIter();
            while (!iter.IsEmpty())
            {
                Pair p = iter.GetHead() as Pair;
                if (p == null)
                    throw new Exception("dispatch requires a list of pairs; types and functiosn");
                Type u = p.Second() as Type;
                if (u == null)
                    throw new Exception("dispatch requires a list of pairs; types and functiosn");
                if (u.IsAssignableFrom(t))
                {
                    Function f = p.First() as Function;
                    if (f == null)
                        throw new Exception("dispatch requires a list of pairs; types and functiosn");
                    f.Eval(exec);
                    return;
                }
                iter = iter.GotoNext();
            }
            throw new Exception("could not find appropriate function to dispatch to");
        }
    }

    public class MetaCommands
    {
        public class Load : PrimitiveFunction
        {
            public Load()
                : base("#load", "(string ~> )", "loads and executes a source code file")
            { }

            public override void Eval(Executor exec)
            {
                exec.LoadModule(exec.PopString());
            }
        }

        public class Save : PrimitiveFunction
        {
            public Save()
                : base("#save", "(string ~> )", "saves a transcript of the session so far")
            { }

            public override void Eval(Executor exec)
            {
                MainClass.SaveTranscript(exec.PopString());
            }
        }

        public class Defs : PrimitiveFunction
        {
            public Defs()
                : base("#defs", "( ~> )", "lists all loaded definitions")
            { }

            public override void Eval(Executor exec)
            {
                MainClass.OutputDefs(exec);
            }
        }

        public class TypeOf : PrimitiveFunction
        {
            public TypeOf()
                : base("#t", "(function -> )", "experimental")
            { }

            public override void Eval(Executor exec)
            {
                Renamer.ResetId();
                QuotedFunction f = exec.TypedPop<QuotedFunction>();
                Config.gbVerboseInference = true;
                CatFxnType ft = TypeInferer.Infer(f.GetChildren(), false);
                if (ft == null)
                    MainClass.WriteLine("type could not be inferred");
                else
                    MainClass.WriteLine(f.ToString() + " : " + ft.ToString());
            }
        }

        public class AllTypes : PrimitiveFunction
        {
            public AllTypes()
                : base("#at", "(function -> )", "experimental")
            { }

            public override void Eval(Executor exec)
            {
                foreach (Function f in exec.GetGlobalScope().GetAllFunctions())
                {
                    string s = f.GetTypeString();
                    if (!s.Equals("untyped"))
                    {
                        try
                        {
                            CatFxnType t = CatFxnType.Create(s);
                            MainClass.WriteLine(f.GetName() + "\t" + s + "\t" + t.ToString());
                        }
                        catch (Exception e)
                        {
                            MainClass.WriteLine(f.GetName() + "\t" + s + "\t" + "error:" + e.Message);
                        }
                    }
                }
            }
        }

        public class Help : PrimitiveFunction
        {
            public Help()
                : base("#help", "( ~> )", "prints some helpful tips")
            { }

            public override void Eval(Executor exec)
            {
                MainClass.WriteLine("The following are some useful commands:");
                MainClass.WriteLine("  \"filename\" #load - loads and executes a Cat file");
                MainClass.WriteLine("  \"filename\" #save - saves a transcript of session");
                MainClass.WriteLine("  #exit - exits the interpreter.");
                MainClass.WriteLine("  #defs - lists available functions.");
                MainClass.WriteLine("  [...] #t - attempts to infer the type of a quotation");
                MainClass.WriteLine("  \"command\" #h  - provides more information about a command.");
                MainClass.WriteLine("  clr - clears the stack");
            }
        }

        public class CommandHelp : PrimitiveFunction
        {
            public CommandHelp()
                : base("#h", "(string ~> )", "prints help about a command")
            { }

            public override void Eval(Executor exec)
            {
                Function f = exec.GetGlobalScope().Lookup(exec.PopString());
                if (f != null)
                {
                    MainClass.WriteLine(f.GetName() + "\t" + f.GetTypeString() + "\t" + f.GetDesc());
                }
                else
                {
                    MainClass.WriteLine(exec.PopString() + " is not defined");
                }
            }
        }

        public class Expand : PrimitiveFunction
        {
            public Expand()
                : base("#expand", "('A -> 'B) ~> ('A -> 'B)", "makes an inline expansion of a function")
            { }

            public override void Eval(Executor exec)
            {
                Function f = exec.PopFunction();
                List<Function> list = new List<Function>();
                f.Expand(list);
                QuotedFunction q = new QuotedFunction(list);
                exec.Push(q);
            }
        }

        public class ApplyMacros : PrimitiveFunction
        {
            public ApplyMacros()
                : base("#m", "('A -> 'B) ~> ('A -> 'B)", "applies macros to a function")
            { }

            public override void Eval(Executor exec)
            {
                QuotedFunction f = exec.TypedPop<QuotedFunction>();
                List<Function> list = new List<Function>(f.GetChildren().ToArray());
                Macros.GetGlobalMacros().ApplyMacros(list);               
                QuotedFunction q = new QuotedFunction(list);
                exec.Push(q);
            }
        }

        public class Compile : PrimitiveFunction
        {
            public Compile()
                : base("#c", "(('A -> 'B) -> Compilation)", "compiles a function")
            { 
            }

            public override void Eval(Executor exec)
            {
                QuotedFunction f = exec.TypedPop<QuotedFunction>();
                List<Function> list = new List<Function>(f.GetChildren().ToArray());
                Compilation c = new Compilation();
                c.Compile(list);
                exec.Push(c);
            }
        }

        public class Execute : PrimitiveFunction
        {
            public Execute()
                : base("#exec", "(Compilation ~> 'B)", "executes a compiled function")
            {
            }

            public override void Eval(Executor exec)
            {
                Compilation c = exec.TypedPop<Compilation>();
                c.InvokeDefault(exec);
            }
        }

        public class PartialEval : PrimitiveFunction
        {
            public PartialEval()
                : base("#pe", "('A -> 'B) -> ('A -> 'B)", "reduces a function through partial evaluation")
            {
            }

            public override void Eval(Executor exec)
            {
                QuotedFunction f = exec.TypedPop<QuotedFunction>();
                List<Function> list = new List<Function>(f.GetChildren().ToArray());
                List<Function> result = PartialEvaluator.Eval(list);
                QuotedFunction q = new QuotedFunction(result);
                exec.Push(q);
            }
        }

        public class GenDocs : PrimitiveFunction
        {
            public GenDocs()
                : base("#gd", "( ~> )", "outputs html documentation")
            {
            }

            public override void Eval(Executor exec)
            {
                string s = exec.PopString();
                CatDocMaker m = new CatDocMaker(s);
            }
        }

    }

    public class Primitives
    {
        #region conversion functions
        public class Str : PrimitiveFunction
        {
            public Str()
                : base("str", "(var -> string)", "converts any value into a string representation.")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(MainClass.ObjectToString(exec.Pop()));
            }
        }

        public class MakeByte : PrimitiveFunction
        {
            public MakeByte()
                : base("to_byte", "(int -> byte)", "converts an integer into a byte, throwing away sign and ignoring higher bits")
            { }

            public override void Eval(Executor exec)
            {
                int n = exec.PopInt();
                byte b = (byte)n;
                exec.Push(b);
            }
        }
        public class BinStr : PrimitiveFunction
        {
            public BinStr()
                : base("bin_str", "(int -> string)", "converts a number into a binary string representation.")
            { }

            public override void Eval(Executor exec)
            {
                int n = exec.PopInt();
                string s = "";

                if (n == 0) s = "0";
                while (n > 0)
                {
                    if (n % 2 == 1)
                    {
                        s = "1" + s;
                    }
                    else
                    {
                        s = "0" + s;
                    }
                    n /= 2;
                }
                exec.Push(n.ToString(s));
            }
        }

        public class HexStr : PrimitiveFunction
        {
            public HexStr()
                : base("hex_str", "(int -> string)", "converts a number into a hexadecimal string representation.")
            { }

            public override void Eval(Executor exec)
            {
                int n = exec.PopInt();
                exec.Push(n.ToString("x"));
            }
        }

        #endregion 

        #region primitive function classes
        public class Id : PrimitiveFunction
        {
            public Id()
                : base("id", "('a -> 'a)", "does nothing, but requires one item on the stack.")
            { }

            public override void Eval(Executor exec)
            {                
            }
        }

        public class Eq : PrimitiveFunction
        {
            public Eq()
                : base("eq", "(var var -> bool)", "returns true if both items on stack are the same type, and have same value")
            { }

            public override void Eval(Executor exec)
            {
                Object x = exec.Pop();
                Object y = exec.Pop();
                exec.Push(x.Equals(y));
            }
        }

        public class Dup : PrimitiveFunction
        {
            public Dup()
                : base("dup", "('R 'a -> 'R 'a 'a)", "duplicate the top item on the stack")
            { }

            public override void Eval(Executor exec)
            {
                if (exec.Peek() is FMutableList)
                {
                    exec.Push((exec.Peek() as FMutableList).Clone());
                }
                else
                {
                    exec.Push(exec.Peek());
                }
            }
        }

        public class Pop : PrimitiveFunction
        {
            public Pop()
                : base("pop", "('R 'a -> 'R)", "removes the top item from the stack")
            { }

            public override void Eval(Executor exec)
            {
                exec.Pop();
            }
        }

        public class Swap : PrimitiveFunction
        {
            public Swap()
                : base("swap", "('R 'a 'b -> 'R 'b 'a)", "swap the top two items on the stack")
            { }

            public override void Eval(Executor exec)
            {
                Object o1 = exec.Pop();
                Object o2 = exec.Pop();
                exec.Push(o1);
                exec.Push(o2);
            }           
        }

        public class Clr : PrimitiveFunction
        {
            public Clr()
                : base("clear", "('A -> )", "removes all items from the stack")
            { }

            public override void Eval(Executor exec)
            {
                exec.GetStack().Clear();
            }
        }
        #endregion

        #region function functions
        public class EvalFxn : PrimitiveFunction
        {
            public EvalFxn()
                : base("eval", "('A ('A -> 'B) -> 'B)", "evaluates a function")
            { }

            public override void Eval(Executor exec)
            {
                Function f = exec.TypedPop<Function>();
                f.Eval(exec);
            }
        }

        public class Dip : PrimitiveFunction
        {
            public Dip()
                : base("dip", "('A 'b ('A -> 'C) -> 'C 'b)", "evaluates function, temporarily removing second item")
            { }

            public override void Eval(Executor exec)
            {
                Function f = exec.TypedPop<Function>();
                Object o = exec.Pop();
                f.Eval(exec);
                exec.Push(o);
            }
        }

        public class Compose : PrimitiveFunction
        {
            public Compose()
                : base("compose", "('R ('A -> 'B) ('B -> 'C) -> 'R ('A -> 'C))",
                    "creates a function by composing (concatenating) two existing functions")
            { }

            public override void Eval(Executor exec)
            {
                QuotedFunction right = exec.TypedPop<QuotedFunction>();
                QuotedFunction left = exec.TypedPop<QuotedFunction>();
                QuotedFunction f = new QuotedFunction(left, right);
                exec.Push(f);
            }
        }

        public class Quote : PrimitiveFunction
        {
            public Quote()
                : base("qv", "('R 'a -> 'R ('S -> 'S 'a))",
                    "short for 'quote value', creates a constant generating function from the top value on the stack")
            { }

            public override void Eval(Executor exec)
            {
                Object o = exec.Pop();
                QuotedValue q = new QuotedValue(o);
                exec.Push(q);
            }
        }

        public class Dispatch3 : Dispatch
        {
            public Dispatch3()
                : base("dispatch3", "(var var var list -> var)")
            { }
        }

        public class Dispatch2 : Dispatch
        {
            public Dispatch2()
                : base("dispatch2", "(var var list -> var)")
            { }
        }

        public class Dispatch1 : Dispatch
        {
            public Dispatch1()
                : base("dispatch1", "(var list -> var)")
            { }
        }

        #endregion

        #region control flow primitives 
        public class While : PrimitiveFunction
        {
            public While()
                : base("while", "(input='A body=('A -> 'A) condition=('A -> 'A bool) -> 'A)",
                    "executes a block of code repeatedly until the condition returns true")
            { }

            public override void Eval(Executor exec)
            {
                Function cond = exec.TypedPop<Function>();
                Function body = exec.TypedPop<Function>();

                cond.Eval(exec);
                while ((bool)exec.Pop())
                {
                    body.Eval(exec);
                    cond.Eval(exec);
                }
            }
        }

        public class If : PrimitiveFunction
        {
            public If()
                : base("if", "('A bool ontrue=('A -> 'B) onfalse=('A -> 'B) -> 'B)",
                    "executes one predicate or another whether the condition is true")
            { }

            public override void Eval(Executor exec)
            {
                Function onfalse = exec.TypedPop<Function>();
                Function ontrue = exec.TypedPop<Function>();

                if ((bool)exec.Pop())
                {
                    ontrue.Eval(exec);
                }
                else
                {
                    onfalse.Eval(exec);
                }
            }
        }

        public class BinRec : PrimitiveFunction
        {
            // The fact that it takes 'b instead of 'B is a minor optimization for untyped implementations
            // I may ignore it later on.
            public BinRec()
                : base("bin_rec", "('a ('a -> 'a bool) ('a -> 'b) ('a -> 'C 'a 'a) ('C 'b 'b -> 'b) -> 'b)",
                    "execute a binary recursion process")
            { }

            public void Helper(Executor exec, Function fResultRelation, Function fArgRelation, Function fBaseCase, Function fCondition)
            {
                fCondition.Eval(exec);
                if (exec.PopBool())
                {
                    fBaseCase.Eval(exec);
                }
                else
                {
                    fArgRelation.Eval(exec);
                    Helper(exec, fResultRelation, fArgRelation, fBaseCase, fCondition);
                    Object o = exec.Pop();
                    Helper(exec, fResultRelation, fArgRelation, fBaseCase, fCondition);
                    exec.Push(o);
                    fResultRelation.Eval(exec);
                }
            }

            public override void Eval(Executor exec)
            {
                Helper(exec, exec.PopFunction(), exec.PopFunction(), exec.PopFunction(), exec.PopFunction());
            }
        }

        public class Throw : PrimitiveFunction
        {
            public Throw()
                : base("throw", "(var -> )", "throws an exception")
            { }

            public override void Eval(Executor exec)
            {
                object o = exec.Pop();
                throw new CatException(o);
            }
        }

        public class TryCatch : PrimitiveFunction
        {
            public TryCatch()
                : base("try_catch", "('A ('A -> 'B) ('A var -> 'B) -> 'B)", "evaluates a function, and catches any exceptions")
            { }

            public override void Eval(Executor exec)
            {
                Function c = exec.TypedPop<Function>();
                Function t = exec.TypedPop<Function>();
                object[] stkCopy = new object[exec.GetStack().Count];
                exec.GetStack().CopyTo(stkCopy);
                try
                {
                    t.Eval(exec);
                }
                catch (CatException e)
                {
                    exec.GetStack().RemoveRange(stkCopy.Length, stkCopy.Length);
                    exec.GetStack().SetRange(0, stkCopy);

                    MainClass.WriteLine("exception caught");

                    exec.Push(e.GetObject());
                    c.Eval(exec);
                }
            }
        }
        #endregion 

        #region boolean functions
        public class True : PrimitiveFunction
        {
            public True()
                : base("true", "( -> bool)", "pushes the boolean value true on the stack")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(true);
            }
        }

        public class False : PrimitiveFunction
        {
            public False()
                : base("false", "( -> bool)", "pushes the boolean value false on the stack")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(false);
            }
        }

        public class And : PrimitiveFunction
        {
            public And()
                : base("and", "(bool bool -> bool)", "returns true if both of the top two values on the stack are true")
            { }

            public override void Eval(Executor exec)
            {
                bool x = (bool)exec.Pop();
                bool y = (bool)exec.Pop();
                exec.Push(x && y);
            }
        }

        public class Or : PrimitiveFunction
        {
            public Or()
                : base("or", "(bool bool -> bool)", "returns true if either of the top two values on the stack are true")
            { }

            public override void Eval(Executor exec)
            {
                bool x = (bool)exec.Pop();
                bool y = (bool)exec.Pop();
                exec.Push(x || y);
            }
        }

        public class Not : PrimitiveFunction
        {
            public Not()
                : base("not", "(bool -> bool)", "returns true if the top value on the stack is false")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(!(bool)exec.Pop());
            }
        }
        #endregion

        #region type functions
        public class TypeId : PrimitiveFunction
        {
            public TypeId()
                : base("type_of", "(var -> type)", "returns a type tag for an object")
            { }

            public override void Eval(Executor exec)
            {
                Object o = exec.Peek();
                if (o is FList)
                {
                    // HACK: this is not the correct type! 
                    exec.Push(typeof(FList));
                }
                else if (o is Function)
                {
                    exec.Push((o as Function).GetFxnType());
                }
                else
                {
                    // HACK: this is not the correct type! 
                    exec.Push(o.GetType());
                }
            }
        }
        public class TypeType : PrimitiveFunction
        {
            public TypeType()
                : base("type", "( -> type)", "")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(typeof(Type));
            }
        }
        public class IntType : PrimitiveFunction
        {
            public IntType()
                : base("int", "( -> type)", "")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(typeof(int));
            }
        }
        public class StrType : PrimitiveFunction
        {
            public StrType()
                : base("string", "( -> type)", "")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(typeof(string));
            }
        }
        public class DblType : PrimitiveFunction
        {
            public DblType()
                : base("double", "( -> type)", "")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(typeof(double));
            }
        }
        public class ByteType : PrimitiveFunction
        {
            public ByteType()
                : base("byte", "( -> type)", "")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(typeof(byte));
            }
        }
        public class BitType : PrimitiveFunction
        {
            public BitType()
                : base("bit", "( -> type)", "")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(typeof(Bit));
            }
        }
        public class BoolType : PrimitiveFunction
        {
            public BoolType()
                : base("bool", "( -> type)", "")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(typeof(Bit));
            }
        }
        public class TypeEq : PrimitiveFunction
        {
            public TypeEq()
                : base("type_eq", "(type type -> bool)", "returns true if either type is assignable to the other")
            { }

            public override void Eval(Executor exec)
            {
                Type t = exec.TypedPop<Type>();
                Type u = exec.TypedPop<Type>();
                exec.Push(t.Equals(u) || u.Equals(t));
            }
        }
        #endregion 

        #region int functions
        public class AddInt : PrimitiveFunction
        {
            public AddInt() : base("add_int", "(int int -> int)", "") { }            
            public override void Eval(Executor exec) { exec.Push(exec.PopInt() + exec.PopInt()); }
        }
        public class MulInt : PrimitiveFunction
        {
            public MulInt() : base("mul_int", "(int int -> int)", "") { }
            public override void Eval(Executor exec) { exec.Push(exec.PopInt() * exec.PopInt()); }
        }
        public class DivInt : PrimitiveFunction
        {
            public DivInt() : base("div_int", "(int int -> int)", "") { }
            public override void Eval(Executor exec) { exec.Swap(); exec.Push(exec.PopInt() / exec.PopInt()); }
        }
        public class SubInt : PrimitiveFunction
        {
            public SubInt() : base("sub_int", "(int int -> int)", "") { }
            public override void Eval(Executor exec) { exec.Swap();  exec.Push(exec.PopInt() - exec.PopInt()); }
        }
        public class ModInt : PrimitiveFunction
        {
            public ModInt() : base("mod_int", "(int int -> int)", "") { }
            public override void Eval(Executor exec) { exec.Swap();  exec.Push(exec.PopInt() % exec.PopInt()); }
        }
        public class NegInt : PrimitiveFunction
        {
            public NegInt() : base("neg_int", "(int -> int)", "") { }
            public override void Eval(Executor exec) { exec.Push(-exec.PopInt()); }
        }
        public class ComplInt : PrimitiveFunction
        {
            public ComplInt() : base("compl_int", "(int -> int)", "") { }
            public override void Eval(Executor exec) { exec.Push(~exec.PopInt()); }
        }
        public class ShlInt : PrimitiveFunction
        {
            public ShlInt() : base("shl_int", "(int int -> int)", "") { }
            public override void Eval(Executor exec) { exec.Swap(); exec.Push(exec.PopInt() << exec.PopInt()); }
        }
        public class ShrInt : PrimitiveFunction
        {
            public ShrInt() : base("shr_int", "(int int -> int)", "") { }
            public override void Eval(Executor exec) { exec.Swap(); exec.Push(exec.PopInt() >> exec.PopInt()); }
        }
        public class GtInt : PrimitiveFunction
        {
            public GtInt() : base("gt_int", "(int int -> bool)", "") { }
            public override void Eval(Executor exec) { exec.Swap(); exec.Push(exec.PopInt() > exec.PopInt()); }
        }
        public class LtInt : PrimitiveFunction
        {
            public LtInt() : base("lt_int", "(int int -> bool)", "") { }
            public override void Eval(Executor exec) { exec.Swap(); exec.Push(exec.PopInt() < exec.PopInt()); }
        }
        public class GtEqInt : PrimitiveFunction
        {
            public GtEqInt() : base("gteq_int", "(int int -> bool)", "") { }
            public override void Eval(Executor exec) { exec.Swap(); exec.Push(exec.PopInt() >= exec.PopInt()); }
        }
        public class LtEqInt : PrimitiveFunction
        {
            public LtEqInt() : base("lteq_int", "(int int -> bool)", "") { }
            public override void Eval(Executor exec) { exec.Swap();  exec.Push(exec.PopInt() <= exec.PopInt()); }
        }
        #endregion

        #region byte functions
        public static byte add_byte(byte x, byte y) { return (byte)(x + y); }
        public static byte sub_byte(byte x, byte y) { return (byte)(x - y); }
        public static byte div_byte(byte x, byte y) { return (byte)(x / y); }
        public static byte mul_byte(byte x, byte y) { return (byte)(x * y); }
        public static byte mod_byte(byte x, byte y) { return (byte)(x % y); }
        public static byte compl_byte(byte x) { return (byte)(~x); }
        public static byte shl_byte(byte x, byte y) { return (byte)(x << y); }
        public static byte shr_byte(byte x, byte y) { return (byte)(x >> y); }
        public static bool gt_byte(byte x, byte y) { return x > y; }
        public static bool lt_byte(byte x, byte y) { return x < y; }
        public static bool gteq_byte(byte x, byte y) { return x >= y; }
        public static bool lteq_byte(byte x, byte y) { return x <= y; }
        #endregion

        #region bit functions
        public struct Bit
        {
            public bool m;
            public Bit(int n) { m = n != 0; }
            public Bit(bool x) { m = x; }
            public Bit add(Bit x) { return new Bit(m ^ x.m); }
            public Bit sub(Bit x) { return new Bit(m && !x.m); }
            public Bit mul(Bit x) { return new Bit(m && !x.m); }
            public Bit div(Bit x) { return new Bit(m && !x.m); }
            public Bit mod(Bit x) { return new Bit(m && !x.m); }
            public bool lteq(Bit x) { return !m || x.m; }
            public bool eq(Bit x) { return m == x.m; }
            public override bool Equals(object obj)
            {
                return (obj is Bit) && (((Bit)obj).m == m);
            }
            public override int GetHashCode()
            {
                return m.GetHashCode();
            }
            public override string ToString()
            {
                return m ? "0b1" : "0b0";
            }
        }
        public static Bit add_bit(Bit x, Bit y) { return x.add(y); }
        public static Bit sub_bit(Bit x, Bit y) { return x.sub(y); }
        public static Bit mul_bit(Bit x, Bit y) { return x.mul(y); }
        public static Bit div_bit(Bit x, Bit y) { return x.div(y); }
        public static Bit mod_bit(Bit x, Bit y) { return x.mod(y); }
        public static Bit compl_bit(Bit x) { return new Bit(!x.m); }
        public static bool neq_bit(Bit x, Bit y) { return !x.eq(y); }
        public static bool gt_bit(Bit x, Bit y) { return !x.lteq(y); }
        public static bool lt_bit(Bit x, Bit y) { return !x.eq(y) && x.lteq(y); }
        public static bool gteq_bit(Bit x, Bit y) { return x.eq(y) || !x.lteq(y); }
        public static bool lteq_bit(Bit x, Bit y) { return x.lteq(y); }
        public static Bit min_bit(Bit x, Bit y) { return new Bit(x.m && y.m); }
        public static Bit max_bit(Bit x, Bit y) { return new Bit(x.m || y.m); }
        #endregion

        #region double functions
        public static double add_dbl(double x, double y) { return x + y; }
        public static double sub_dbl(double x, double y) { return x - y; }
        public static double div_dbl(double x, double y) { return x / y; }
        public static double mul_dbl(double x, double y) { return x * y; }
        public static double mod_dbl(double x, double y) { return x % y; }
        public static double inc_dbl(double x) { return x + 1; }
        public static double dec_dbl(double x) { return x - 1; }
        public static double neg_dbl(double x) { return -x; }
        public static bool gt_dbl(double x, double y) { return x > y; }
        public static bool lt_dbl(double x, double y) { return x < y; }
        public static bool gteq_dbl(double x, double y) { return x >= y; }
        public static bool lteq_dbl(double x, double y) { return x <= y; }
        public static double min_dbl(double x, double y) { return Math.Min(x, y); }
        public static double max_dbl(double x, double y) { return Math.Max(x, y); }
        public static double abs_dbl(double x) { return Math.Abs(x); }
        public static double pow_dbl(double x, double y) { return Math.Pow(x, y); }
        public static double sqr_dbl(double x) { return x * x; }
        public static double sin(double x) { return Math.Sin(x); }
        public static double cos(double x) { return Math.Cos(x); }
        public static double tan(double x) { return Math.Tan(x); }
        public static double asin(double x) { return Math.Asin(x); }
        public static double acos(double x) { return Math.Acos(x); }
        public static double atan(double x) { return Math.Atan(x); }
        public static double atan2(double x, double y) { return Math.Atan2(x, y); }
        public static double sinh(double x) { return Math.Sinh(x); }
        public static double cosh(double x) { return Math.Cosh(x); }
        public static double tanh(double x) { return Math.Tanh(x); }
        public static double sqrt(double x) { return Math.Sqrt(x); }
        public static double trunc(double x) { return Math.Truncate(x); }
        public static double round(double x) { return Math.Round(x); }
        public static double ceil(double x) { return Math.Ceiling(x); }
        public static double floor(double x) { return Math.Floor(x); }
        public static double log(double x, double y) { return Math.Log(x, y); }
        public static double log10(double x) { return Math.Log10(x); }
        public static double ln(double x) { return Math.Log(x); }
        public static double e() { return Math.E; }
        public static double pi() { return Math.PI; }
        public static string format_scientific(double x) { return x.ToString("E"); }
        public static string format_currency(double x) { return x.ToString("C"); }
        #endregion

        #region string functions
        public static bool gt_str(string x, string y) { return x.CompareTo(y) > 0; }
        public static bool lt_str(string x, string y) { return x.CompareTo(y) < 0; }
        public static bool gteq_str(string x, string y) { return x.CompareTo(y) >= 0; }
        public static bool lteq_str(string x, string y) { return x.CompareTo(y) <= 0; }
        public static string min_str(string x, string y) { return lteq_str(x, y) ? x : y; }
        public static string max_str(string x, string y) { return gteq_str(x, y) ? x : y; }
        public static string add_str(string x, string y) { return x + y; }
        public static string sub_str(string x, int i, int n) { return x.Substring(i, n); }
        public static string new_str(char c, int n) { return new string(c, n); }
        public static int index_of(string x, string y) { return x.IndexOf(y); }
        public static string replace_str(string x, string y, string z) { return x.Replace(y, z); }
        #endregion

        #region console functions
        public class Write : PrimitiveFunction
        {
            public Write()
                : base("write", "('a ~> )", "outputs the text representation of a value to the console")
            { }

            public override void Eval(Executor exec)
            {
                MainClass.Write(exec.Pop());
            }
        }

        public class WriteLn : PrimitiveFunction
        {
            public WriteLn()
                : base("writeln", "('a ~> )", "outputs the text representation of a value to the console followed by a newline character")
            { }

            public override void Eval(Executor exec)
            {
                MainClass.WriteLine(exec.Pop());
            }
        }

        public class ReadLn : PrimitiveFunction
        {
            public ReadLn()
                : base("readln", "( ~> string)", "inputs a string from the user (or console)")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(Console.ReadLine());
            }
        }

        public class ReadKey : PrimitiveFunction
        {
            public ReadKey()
                : base("read", "( ~> char)", "inputs a single character from the user (or console)")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(Console.ReadKey().KeyChar);
            }
        }
        #endregion

        #region byte block functions
        public class MakeByteBlock : PrimitiveFunction
        {
            public MakeByteBlock()
                : base("byte_block", "(int -> byte_block)", "creates a mutable array of bytes")
            { }

            public override void Eval(Executor exec)
            {
                int n = exec.PopInt();
                ByteBlock bb = new ByteBlock(n);
                bb.ZeroMemory();
                exec.Push(bb);
            }
        }
        #endregion 

        #region i/o functions
        public class OpenFileReader : PrimitiveFunction
        {
            public OpenFileReader()
                : base("file_reader", "(string -> istream)", "creates an input stream from a file name")
            { }

            public override void Eval(Executor exec)
            {
                string s = exec.PopString();
                exec.Push(File.OpenRead(s));
            }
        }

        public class OpenWriter : PrimitiveFunction
        {
            public OpenWriter()
                : base("file_writer", "(string -> ostream)", "creates an output stream from a file name")
            { }

            public override void Eval(Executor exec)
            {
                string s = exec.PopString();
                exec.Push(File.Create(s));
            }
        }

        public class FileExists : PrimitiveFunction
        {
            public FileExists()
                : base("file_exists", "(string -> string bool)", "returns a boolean value indicating whether a file or directory exists")
            { }

            public override void Eval(Executor exec)
            {
                string s = exec.PeekString();
                exec.Push(Directory.Exists(s));
            }
        }

        public class TmpFileName : PrimitiveFunction
        {
            public TmpFileName()
                : base("temp_file", "( -> string)", "creates a unique temporary file")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(Path.GetTempFileName());
            }
        }

        public class ReadBytes : PrimitiveFunction
        {
            public ReadBytes()
                : base("read_bytes", "(istream int -> istream bytes)", "reads a number of bytes into an array from an input stream")
            { }

            public override void Eval(Executor exec)
            {
                int n = exec.PopInt();
                Stream f = exec.TypedPeek<Stream>();
                byte[] ab = new byte[n];
                f.Read(ab, 0, n);
                exec.Push(new MArray<byte>(ab)); 
            }
        }

        public class WriteBytes : PrimitiveFunction
        {
            public WriteBytes()
                : base("write_bytes", "(ostream bytes -> ostream)", "writes a byte array to an output stream")
            { }

            public override void Eval(Executor exec)
            {
                MArray<byte> mb = exec.TypedPop<MArray<byte>>();
                Stream f = exec.TypedPeek<Stream>();
                f.Write(mb.m, 0, mb.Count());
            }
        }

        public class CloseStream : PrimitiveFunction
        {
            public CloseStream()
                : base("close_stream", "(stream -> )", "closes a stream")
            { }

            public override void Eval(Executor exec)
            {
                Stream f = exec.TypedPop<Stream>();
                f.Close();
                f.Dispose();
            }
        }
        #endregion

        #region hash functions
        public class MakeHashList : PrimitiveFunction
        {
            public MakeHashList()
                : base("hash_list", "( -> hash_list)", "makes an empty hash list")
            { }

            public override void Eval(Executor exec)
            {
                exec.Push(new HashList());
            }
        }

        public class HashGet : PrimitiveFunction
        {
            public HashGet()
                : base("hash_get", "(hash_list var -> hash_list var)", "gets a value from a hash list using a key")
            { }

            public override void Eval(Executor exec)
            {
                Object key = exec.Pop();
                HashList hash = exec.TypedPeek<HashList>();
                Object value = hash.Get(key);
                exec.Push(value);
            }
        }

        public class HashSet : PrimitiveFunction
        {
            public HashSet()
                : base("hash_set", "(hash_list key=var value=var -> hash_list)", "associates a value with a key in a hash list")
            { }

            public override void Eval(Executor exec)
            {
                Object value = exec.Pop();
                Object key = exec.Pop();
                HashList hash = exec.TypedPop<HashList>();
                exec.Push(hash.Set(key, value));
            }
        }

        public class HashAdd : PrimitiveFunction
        {
            public HashAdd()
                : base("hash_add", "(hash_list key=var value=var -> hash_list)", "associates a value with a key in a hash list")
            { }

            public override void Eval(Executor exec)
            {
                Object value = exec.Pop();
                Object key = exec.Pop();
                HashList hash = exec.TypedPop<HashList>();
                exec.Push(hash.Add(key, value));
            }
        }

        public class HashContains : PrimitiveFunction
        {
            public HashContains()
                : base("hash_contains", "(hash_list key=var -> hash_list bool)", "returns true if hash list contains key")
            { }

            public override void Eval(Executor exec)
            {
                Object key = exec.Pop();
                HashList hash = exec.TypedPeek<HashList>();
                exec.Push(hash.ContainsKey(key));
            }
        }

        public class HashToList : PrimitiveFunction
        {
            public HashToList()
                : base("hash_to_list", "(hash_list -> list)", "converts a hash_list to a list of pairs")
            { }

            public override void Eval(Executor exec)
            {
                HashList hash = exec.TypedPop<HashList>();
                exec.Push(hash.ToArray());
            }
        }
        #endregion 

        #region list functions
        public class List : PrimitiveFunction
        {
            public List()
                : base("to_list", "(( -> 'A) -> list)", "creates a list from a function")
            { }

            public override void Eval(Executor exec)
            {
                Function f = exec.TypedPop<Function>();
                f.Eval(GetExecutor());
                exec.Push(GetExecutor().GetStack().ToList());
                GetExecutor().GetStack().Clear();
            }
        }

        public class IsEmpty : PrimitiveFunction
        {
            public IsEmpty()
                : base("empty", "(list -> list bool)", "returns true if the list is empty")
            { }

            public override void Eval(Executor exec)
            {
                FList list = exec.TypedPeek<FList>();
                exec.Push(list.IsEmpty());
            }
        }

        public class Count : PrimitiveFunction
        {
            public Count()
                : base("count", "(list -> list int)", "returns the number of items in a list")
            { }

            public override void Eval(Executor exec)
            {
                FList list = exec.TypedPeek<FList>();
                exec.Push(list.Count());
            }
        }

        public class Nth : PrimitiveFunction
        {
            public Nth()
                : base("nth", "(list int -> list var)", "returns the nth item in a list")
            { }

            public override void Eval(Executor exec)
            {
                int n = exec.PopInt();
                FList list = exec.TypedPeek<FList>();
                exec.Push(list.Nth(n));
            }
        }

        public class Gen : PrimitiveFunction
        {
            public Gen()
                : base("gen", "(init='a next=('a -> 'a) cond=('a -> bool) -> list)",
                    "creates a lazily evaluated list")
            { }

            public override void Eval(Executor exec)
            {
                Function term = exec.TypedPop<Function>();
                Function next = exec.TypedPop<Function>();
                Object init = exec.Pop();
                exec.Push(new Generator(init, next.ToMapFxn(), term.ToFilterFxn()));
            }
        }

        public class Nil : PrimitiveFunction
        {
            public Nil()
                : base("nil", "( -> list)", "creates an empty list")
            { }

            public override void  Eval(Executor exec)
            {
 	            exec.Push(FList.Nil());
            }
        }

        public class Unit : PrimitiveFunction
        {
            public Unit()
                : base("unit", "('a -> list)", "creates a list of one item")
            { }

            public override void  Eval(Executor exec)
            {
 	            exec.Push(FList.MakeUnit(exec.Pop()));
            }
        }

        public class MakePair : PrimitiveFunction
        {
            public MakePair()
                : base("pair", "('second 'first -> list)", "creates a list from two items")
            { }

            public override void Eval(Executor exec)
            {
                Object x = exec.Pop();
                Object y = exec.Pop();
 	            exec.Push(FList.MakePair(x, y));
            }
        }

        public class Cons : PrimitiveFunction
        {
            public Cons()
                : base("cons", "(list 'a -> list)", "prepends an item to a list")
            { }

            public override void Eval(Executor exec)
            {
                object x = exec.Pop();
                FList list = exec.TypedPop<FList>();
 	            exec.Push(FList.Cons(x, list));
            }
        }

        public class Head : PrimitiveFunction
        {
            public Head()
                : base("head", "(list -> var)", "replaces a list with the first item")
            { }

            public override void Eval(Executor exec)
            {
                FList list = exec.TypedPop<FList>();
 	            exec.Push(list.GetHead());
            }
        }

        public class First : PrimitiveFunction
        {
            public First()
                : base("first", "(list -> list var)", "gets the first item from a list")
            { }

            public override void Eval(Executor exec)
            {
                FList list = exec.TypedPeek<FList>();
                exec.Push(list.GetHead());
            }
        }

        public class Last : PrimitiveFunction
        {
            public Last()
                : base("last", "(list -> list var)", "gets the last item from a list")
            { }

            public override void Eval(Executor exec)
            {
                FList list = exec.TypedPeek<FList>();
 	            exec.Push(list.Last());
            }
        }

        public class Tail : PrimitiveFunction
        {
            public Tail()
                : base("tail", "(list -> list)", "removes first item from a list")
            { }

            public override void Eval(Executor exec)
            {
                FList list = exec.TypedPop<FList>();
                exec.Push(list.Tail());
            }
        }

        public class Rest : PrimitiveFunction
        {
            public Rest()
                : base("rest", "(list -> list list)", "gets a copy of the list with one item")
            { }

            public override void Eval(Executor exec)
            {
                FList list = exec.TypedPeek<FList>();
                exec.Push(list.Tail());
            }
        }

        public class Uncons : PrimitiveFunction
        {
            public Uncons()
                : base("uncons", "(list -> list var)", "returns the top of the list, and the rest of a list")
            {}

            public override void Eval(Executor exec)
            {
                FList list = exec.TypedPop<FList>();
                exec.Push(list.Tail());
                exec.Push(list.GetHead());
            }
        }

        public class Map : PrimitiveFunction
        {
            public Map()
                : base("map", "(list ('a -> 'b) -> list)", "creates a new list by modifying an existing list")
            { }

            public override void Eval(Executor exec)
            {
                Function f = exec.TypedPop<Function>();
                FList list = exec.TypedPop<FList>();
                exec.Push(list.Map(f.ToMapFxn()));
            }
        }

        public class Filter : PrimitiveFunction
        {
            public Filter()
                : base("filter", "(list ('a -> bool) -> list)", "creates a new list containing elements that pass the condition")
            { }

            public override void Eval(Executor exec)
            {
                Function f = exec.TypedPop<Function>();
                FList list = exec.TypedPop<FList>();
                exec.Push(list.Filter(f.ToFilterFxn()));
            }
        }
        public class Fold : PrimitiveFunction
        {
            public Fold()
                : base("gfold", "('A list ('A var -> 'A) -> 'A)", "recursively applies a function to each element in a list")
            { }

            public override void Eval(Executor exec)
            {
                Function f = exec.TypedPop<Function>();
                FList list = exec.TypedPop<FList>();
                FList iter = list.GetIter();
                while (!iter.IsEmpty())
                {
                    exec.Push(iter.GetHead());
                    f.Eval(exec);
                    iter = iter.GotoNext();
                }
            }
        }

        public class Cat : PrimitiveFunction
        {
            public Cat()
                : base("cat", "(list list -> list)", "concatenates two lists")
            { }

            public override void Eval(Executor exec)
            {
                FList first = exec.TypedPop<FList>();
                FList second = exec.TypedPop<FList>();
                exec.Push(FList.Concat(first, second));
            }
        }

        public class TakeN : PrimitiveFunction
        {
            public TakeN()
                : base("take", "(list int -> list)", "creates a new list from the first n items")
            { }

            public override void Eval(Executor exec)
            {
                int n = exec.PopInt();
                FList list = exec.TypedPop<FList>();
                exec.Push(list.TakeN(n));
            }
        }

        public class DropN : PrimitiveFunction
        {
            public DropN()
                : base("drop", "(list int -> list)", "creates a new list without the first n items")
            { }

            public override void Eval(Executor exec)
            {
                int n = exec.PopInt();
                FList list = exec.TypedPop<FList>();
                exec.Push(list.DropN(n));
            }
        }

        public class TakeRange : PrimitiveFunction
        {
            public TakeRange()
                : base("take_range", "(list first=int count=int -> list)", "creates a new list which is a sub-range of the original")
            { }

            public override void Eval(Executor exec)
            {
                int count = exec.PopInt();
                int n = exec.PopInt();
                FList list = exec.TypedPop<FList>();
                exec.Push(list.TakeRange(n, count));
            }
        }

        public class TakeWhile : PrimitiveFunction
        {
            public TakeWhile()
                : base("take_while", "(list ('a -> bool) -> list)", "creates a new list by taking items while the predicate is true")
            { }

            public override void Eval(Executor exec)
            {
                Function f = exec.TypedPop<Function>();
                FList list = exec.TypedPop<FList>();
                exec.Push(list.TakeWhile(f.ToFilterFxn()));
            }
        }

        public class DropWhile : PrimitiveFunction
        {
            public DropWhile()
                : base("drop_while", "(list ('a -> bool) -> list)", "creates a new list by dropping items while the predicate is true")
            { }

            public override void Eval(Executor exec)
            {
                Function f = exec.TypedPop<Function>();
                FList list = exec.TypedPop<FList>();
                exec.Push(list.DropWhile(f.ToFilterFxn()));
            }
        }

        public class CountWhile : PrimitiveFunction
        {
            public CountWhile()
                : base("count_while", "(list ('a -> bool) -> list count)", "creates a new list by dropping items while the predicate is true")
            { }

            public override void Eval(Executor exec)
            {
                Function f = exec.TypedPop<Function>();
                FList list = exec.TypedPeek<FList>();
                exec.Push(list.CountWhile(f.ToFilterFxn()));
            }
        }

        public class RangeGen : PrimitiveFunction
        {
            public RangeGen()
                : base("range_gen", "(int int (int -> 'a) -> list)", 
                    "creates a lazy list from a range of numbers and a generating function")
            { }

            public override void Eval(Executor exec)
            {
                Function f = exec.TypedPop<Function>();
                int count = exec.PopInt();
                int n = exec.PopInt();
                exec.Push(FList.RangeGen(f.ToRangeGenFxn(), n, count));
            }
        }

        public class Repeater : PrimitiveFunction
        {
            public Repeater()
                : base("repeater", "(var -> list)", 
                    "creates a lazy list by repeating a value over and over again")
            { }

            public override void Eval(Executor exec)
            {
                Object o = exec.Pop();
                exec.Push(FList.MakeRepeater(o));
            }
        }

        public class Flatten : PrimitiveFunction
        {
            public Flatten()
                : base("flatten", "(list -> list)", "concatenates all sub-lists in a list of lists")
            { }

            public override void Eval(Executor exec)
            {
                FList list = exec.TypedPop<FList>();
                exec.Push(list.Flatten());
            }
        }
        #endregion

        #region mutable list instructions
        public class SetAt : PrimitiveFunction
        {
            public SetAt()
                : base("set_at", "(list var int -> list)", "sets an item in a mutable list")
            { }

            public override void Eval(Executor exec)
            {
                int n = exec.PopInt();
                Object o = exec.Pop();
                if (exec.Peek() is FMutableList)
                {
                    FMutableList list = exec.TypedPeek<FMutableList>();
                    list.Set(n, o);
                }
                else
                {
                    FList list = exec.TypedPop<FList>();
                    FMutableList mut = new MArray<Object>(list);
                }
            }
        }
        #endregion 
    }
}