/// Public domain code by Christopher Diggins
/// http://www.cat-language.com

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Cat
{
    /// <summary>
    /// Manages the main stack and exposes functions for manipulating them.
    /// </summary>
    public class Executor 
    {
        #region fields
        static public Executor Main = new Executor();
        static public Executor Aux = new Executor();
        private CatStack stack = new CatStack();
        public TextReader input = Console.In;
        public TextWriter output = Console.Out;
        Scope scope = new Scope();
        #endregion

        #region public functions
        public CatStack GetStack()
        {
            return stack;
        }
        public void Push(Object o)
        {
            stack.Push(o);
        }
        public void PushInt(int n)
        {
            stack.Push(n);
        }
        public void PushString(string s)
        {
            stack.Push(s);
        }
        public void PushRef(Function p)
        {
            stack.Push(p);
        }
        public Object Pop()
        {
            return stack.Pop();
        }
        public T TypedPop<T>()
        {
            if (stack.Count == 0)
                throw new Exception("Trying to pop an empty stack");
            Object o = stack.Pop();
            if (!(o is T))
                throw new Exception("Expected type " + typeof(T).Name + " but instead found " + o.GetType().Name);
            return (T)o;
        }
        public int PopInt()
        {
            return TypedPop<int>();
        }
        public bool PopBool()
        {
            return TypedPop<bool>();
        }
        public Function PopFunction()
        {
            return TypedPop<Function>();
        }
        public String PopString()
        {
            return TypedPop<String>();
        }
        public Object Peek()
        {
            return stack.Peek();
        }
        public T TypedPeek<T>()
        {
            if (stack.Count == 0)
                throw new Exception("Trying to peek into an empty stack ");
            Object o = stack.Peek();
            if (!(o is T))
                throw new Exception("Expected type " + typeof(T).Name + " but instead found " + o.GetType().Name);
            return (T)o;
        }
        public Function PeekProgram()
        {
            return TypedPeek<Function>();
        }
        public String PeekString()
        {
            return TypedPeek<String>();
        }
        public bool IsEmpty()
        {
            return (stack.Count == 0);
        }
        #endregion

        #region environment serialization
        public Scope GetGlobalScope()
        {
            return scope;
        }
        public void Import()
        {
            LoadModule(PopString());
        }
        public void LoadModule(string s)
        {
            try
            {
                // Read the file 
                System.IO.StreamReader file = new System.IO.StreamReader(s);
                try
                {
                    string sInput = file.ReadToEnd();
                    Execute(sInput);
                }
                finally 
                {
                    file.Close();
                }
            }
            catch (Exception e)
            {
                MainClass.WriteLine("Failed to load \"" + s + "\"");
                MainClass.WriteLine("Error: {0}", e.Message);
            }
        }
        #endregion

        #region exection functions
        public void Execute(string s)
        {
            try
            {
                Parse(s);
            }
            catch (Exception e)
            {
                MainClass.WriteLine(e.Message);
            }
        }
        #endregion

        #region utility functions
        public string StackToString(CatStack stk)
        {
            if (stk.Count == 0) return "_empty_";
            string s = "";
            int nMax = 5;
            if (stk.Count > nMax)
                s = "...";
            if (stk.Count < nMax)
                nMax = stk.Count;
            for (int i = nMax - 1; i >= 0; --i)
            {
                Object o = stk[i];
                s += MainClass.ObjectToString(o) + " ";
            }
            return s;
        }
        public void OutputStack()
        {
            MainClass.WriteLine("stack: " + StackToString(stack));
        }
        #endregion

        #region parsing functions
        private Quotation MakeQuoteFunction(AstQuote node)
        {
            List<Function> fxns = new List<Function>();
            foreach (AstExpr child in node.Terms)
                fxns.Add(ExprToFunction(child));
            return new Quotation(fxns);
        }

        private Function ExprToFunction(AstExpr node)
        {
            if (node is AstInt)
                return new IntFunction((node as AstInt).GetValue());
            if (node is AstFloat)
                return new FloatFunction((node as AstFloat).GetValue());
            if (node is AstString)
                return new StringFunction((node as AstString).GetValue());
            if (node is AstChar)
                return new CharFunction((node as AstChar).GetValue());
            if (node is AstName)
                return new FunctionName(node.ToString());
            if (node is AstQuote)
                return MakeQuoteFunction(node as AstQuote);
            throw new Exception("node " + node.ToString() + " does not have associated function");
        }

        private void ProcessDefinition(AstDef node)
        {
            if (Config.gbAllowNamedParams)
                CatPointFreeForm.Convert(node);
            else if (node.mParams.Count > 0)
                throw new Exception("named parameters are not enabled");
            List<Function> fxns = new List<Function>();
            foreach (AstExpr term in node.mTerms)
                fxns.Add(ExprToFunction(term));
            DefinedFunction def = new DefinedFunction(node.mName, fxns);
            Executor.Main.GetGlobalScope().AddFunction(def);
        }

        private void ProcessNode(CatAstNode node)
        {   
            if (node is AstExpr)
            {
                Function f = ExprToFunction(node as AstExpr);
                f.Eval(this);
            }
            else if (node is AstDef)
            {
                ProcessDefinition(node as AstDef);
            }
            else
            {
                throw new Exception("Unhandled AST node type " + node.GetLabel());
            }
        }

        public void Parse(string s)
        {
            Peg.Parser parser = new Peg.Parser(s);
            bool bResult = parser.Parse(CatGrammar.Line());
            if (!bResult)
                throw new Exception("failed to parse: " + s);
            Peg.AstNode node = parser.GetAst();

            foreach (Peg.AstNode child in node.GetChildren())
                ProcessNode(CatAstNode.Create(child));
        }
        #endregion

    }
}
