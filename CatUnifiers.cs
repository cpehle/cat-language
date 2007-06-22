/// Dedicated to the public domain by Christopher Diggins
/// http://creativecommons.org/licenses/publicdomain/

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Cat
{
    public class Unifiers
    {
        List<List<CatKind>> mConstraintListList = new List<List<CatKind>>();
        Dictionary<string, List<CatKind>> mConstraints = new Dictionary<string, List<CatKind>>();
        Dictionary<string, CatKind> mUnifiers = new Dictionary<string, CatKind>();

        public void AddVectorConstraint(CatTypeVector v1, CatTypeVector v2)
        {
            while (!v1.IsEmpty() && !v2.IsEmpty())
            {
                // DEBUG: uncomment the next line
                CheckConstraints();

                CatKind k1 = v1.GetTop();
                CatKind k2 = v2.GetTop();

                if (k1 is CatStackVar)
                {
                    AddConstraint(k1.ToString(), v2);
                    if (k2 is CatStackVar)
                        AddConstraint(k2.ToString(), v1);
                    return;
                }
                else if (k2 is CatStackVar)
                {
                    AddConstraint(k2.ToString(), v1);
                    return;
                }

                if (k1 is CatTypeVar)
                {
                    AddConstraint(k1.ToString(), k2);
                }
                if (k2 is CatTypeVar)
                {
                    AddConstraint(k2.ToString(), k1);
                }
                if ((k1 is CatFxnType) && (k2 is CatFxnType))
                {
                    AddFxnConstraint(k1 as CatFxnType, k2 as CatFxnType);
                }

                // The problem here is that I need to replace a CatSimpleTypeKind 
                // with a variable, and unify it. Complicated isn't it! 

                if (k1 is CatSimpleTypeKind && !(k2 is CatTypeVar))
                {
                    if (!k2.IsSubtypeOf(k1) && !k1.IsSubtypeOf(k2))
                        throw new KindException(k1, k2);     
                   
                    // TODO: create unifiers for simple types 
                }
                if (k2 is CatSimpleTypeKind && !(k1 is CatTypeVar))
                {
                    if (!k2.IsSubtypeOf(k1) && !k1.IsSubtypeOf(k2))
                        throw new KindException(k1, k2);

                    // TODO: create unifiers for simple types 
                }

                v1 = v1.GetRest();
                v2 = v2.GetRest();
            }
        }

        public void AddFxnConstraint(CatFxnType f1, CatFxnType f2)
        {
            CheckConstraints();

            if (f1 is CatSelfType || f2 is CatSelfType)
                return;

            AddVectorConstraint(f1.GetCons(), f2.GetCons());
            CheckConstraints();

            AddVectorConstraint(f1.GetProd(), f2.GetProd());
            CheckConstraints();
        }

        /// <summary>
        /// Merges constraints associated with "s" into the destination list 
        /// </summary>
        private void MergeConstraintListsIfNeccessary(string s, List<CatKind> dest)
        {
            if (!mConstraints.ContainsKey(s))
                return;

            List<CatKind> src = mConstraints[s];

            // This is a possibility. We want to make sure it doesn't happen
            if (dest == src)
                return;

            // Remove the source constraint list before we continue
            mConstraintListList.Remove(src);

            // Update all hash lists to point to the new destination 
            List<string> keys = new List<string>(mConstraints.Keys);
            foreach (string key in keys)
            {
                if (mConstraints[key] == src)
                    mConstraints[key] = dest;
            }

            // One by one add the elements from the source list to the destination
            while (src.Count > 0) 
            {
                AddConstraintToList(dest, src[0]);
                src.RemoveAt(0);
            }                    
        }

        private void AddConstraintToList(List<CatKind> list, CatKind k)
        {
            if (k is CatTypeVector)
            {
                CatTypeVector v = (k as CatTypeVector);
                Trace.Assert(v.GetKinds().Count > 1);
            }

            if (list.Contains(k))
                return;

            if (k is CatFxnType)
            {
                for (int i=0; i < list.Count; ++i)
                {
                    if (list[i] is CatFxnType)
                    {
                        AddFxnConstraint(k as CatFxnType, list[i] as CatFxnType);
                    }
                }
            }
            else if (k is CatTypeVector)
            {
                for (int i = 0; i < list.Count; ++i)
                {
                    if (list[i] is CatTypeVector)
                    {
                        AddVectorConstraint(k as CatTypeVector, list[i] as CatTypeVector);
                    }
                }
            }

            list.Add(k);

            if (k is CatKind)
            {
                // We may have to merge two constraint lists
                MergeConstraintListsIfNeccessary(k.ToString(), list);
            }

            CheckConstraints();
        }

        /// <summary>
        /// Debugging paranoia.
        /// </summary>
        private void CheckConstraints()
        {
            /*
            foreach (List<CatKind> list in mConstraintListList)
            {
                if (!mConstraints.ContainsValue(list))
                    throw new Exception("internal constraint error");

                foreach (CatKind k in list)
                {
                    if (k.IsKindVar())
                    {
                        if (!mConstraints.ContainsKey(k.ToString()))
                            throw new Exception("internal constraint error");

                        if (mConstraints[k.ToString()] != list)
                            throw new Exception("internal constraint error");
                    }
                }
            }
            
            foreach (List<CatKind> list in mConstraints.Values)
            {
                if (!mConstraintListList.Contains(list))
                    throw new Exception("internal constraints error");
            }
             */
        }

        public void AddConstraint(string s, CatKind k)
        {
            // Don't add self-referential variables 
            if (k.ToString().Equals(s))
                return;

            // Check for single unit vectors 
            if (k is CatTypeVector)
            {
                CatTypeVector vec = k as CatTypeVector;
                if (vec.GetKinds().Count == 1)
                {
                    // vectors with only one thing, are really that thing. 
                    AddConstraint(s, vec.GetKinds()[0]);
                    return;
                }
            }

            // If a constraint list doesn't exist then create one
            if (!mConstraints.ContainsKey(s))
            {
                List<CatKind> list = new List<CatKind>();
                mConstraints.Add(s, list);
                mConstraintListList.Add(list);
            }

            // Check if we are constraining a variable to a variable.
            // If so, we need to share a constraint list 
            if (k.IsKindVar())
            {
                if (mConstraints.ContainsKey(k.ToString()))
                {
                    List<CatKind> c1 = mConstraints[s];
                    List<CatKind> c2 = mConstraints[k.ToString()];

                    // It is important to remember that c1 can be the same as c2
                    if (c1 != c2)
                    {
                        // Copy the contents first 
                        foreach (CatKind tmp in c2.ToArray())
                            AddConstraintToList(c1, tmp);
                    }
                }
                else
                {
                    mConstraints.Add(k.ToString(), mConstraints[s]);
                }

                // Link the other variable to the merged constraint list
                mConstraints[k.ToString()] = mConstraints[s];
            }

            AddConstraintToList(mConstraints[s], k);
            
            // DEBUG: uncomment the next line
            CheckConstraints();
        }

        private CatKind CreateUnifier(CatKind k1, CatKind k2)
        {
            if (k1 == null)
                return k2;
            if (k2 == null)
                return k1;

            if ((k1 is CatFxnType) || (k2 is CatFxnType))
            {
                if (!(k1 is CatFxnType)) return k2;
                if (!(k2 is CatFxnType)) return k1;
                CatFxnType ft1 = k1 as CatFxnType;
                CatFxnType ft2 = k2 as CatFxnType;
                if (ft1.GetCons().GetKinds().Count >= ft2.GetCons().GetKinds().Count) 
                    return ft1;
                else
                    return ft2;
            }
            else if ((k1 is CatTypeVector) || (k2 is CatTypeVector))
            {
                if (!(k1 is CatTypeVector)) return k2;
                if (!(k2 is CatTypeVector)) return k1;
                CatTypeVector vec1 = k1 as CatTypeVector;
                CatTypeVector vec2 = k2 as CatTypeVector;                
                if (vec1.GetKinds().Count >= vec2.GetKinds().Count)
                    return vec1;
                else
                    return vec2;
            }
            else if (k1.IsKindVar())
            {
                // TODO: check that they are both the same kind.
                if (k2.IsKindVar())
                {
                    if (k1.ToString().CompareTo(k2.ToString()) <= 0)
                        return k1;
                    else
                        return k2;
                }
                else
                {
                    return k2;
                }
            }
            else if (k2.IsKindVar())
            {
                // TODO: check that they are both the same kind
                return k1;
            }
            else if (k1 is CatSimpleTypeKind)
            {
                string s1 = k1.ToString();
                string s2 = k2.ToString();
                if (!(k2 is CatSimpleTypeKind))
                    throw new Exception(s1 + " is not compatible with " + s2);

                if (k1.IsSubtypeOf(k2))
                {
                    return k1;
                }
                else if (k2.IsSubtypeOf(k1))
                {
                    return k2;
                }
                else
                {
                    if (Config.gbVerboseInference)
                        MainClass.WriteLine("warning unfiying over 'any': " + s1 + " is not compatible with " + s2);
                    return new CatSimpleTypeKind("any");
                }
            }
            else
            {
                throw new Exception("Unsupported kinds " + k1.ToString() + ":" + k1.GetType().ToString()
                    + " and " + k2.ToString() + ":" + k2.GetType().ToString());
            }
        }

        public void OutputConstraints()
        {
            /* Alternative method of outputting constraints
            foreach (KeyValuePair<string, List<CatKind>> kvp in mConstraints)
            {
                string s = kvp.Key + " = ";
                foreach (CatKind k in kvp.Value)
                    s += k.ToString() + " | ";
                MainClass.WriteLine(s);
            }
             */
            foreach (List<CatKind> list in mConstraintListList)
            {
                string s = "";
                for (int i = 0; i < list.Count; ++i)
                {
                    if (i > 0) s += " = ";
                    s += list[i].ToString();
                }
                MainClass.WriteLine(s);
            }
        }

        private void CreateUnifiers()
        {
            foreach (KeyValuePair<string, List<CatKind>> kvp in mConstraints)
            {
                List<CatKind> list = kvp.Value;

                // Empty lists are not supposed to be possible.
                Trace.Assert(list.Count > 0);

                while (list.Count > 1)
                {
                    if (Config.gbVerboseInference)
                    {
                        MainClass.Write("Merging constraints: ");
                        for (int i = 0; i < list.Count; ++i)
                        {
                            if (i > 0) MainClass.Write(" = ");
                            MainClass.Write(list[i].ToString());
                        }
                        MainClass.WriteLine("");
                    }

                    CatKind k1 = list[0];
                    CatKind k2 = list[1];
                    
                    // Unify both types.
                    CatKind u = CreateUnifier(k1, k2);

                    list.RemoveAt(0);
                    list[0] = u;
                }

                CatKind k = list[0];
                Trace.Assert(k != null);
                
                if (Config.gbVerboseInference)
                    MainClass.WriteLine("Unified constraint = " + k.ToString());

                mUnifiers[kvp.Key] = ResolveKind(k);

                Trace.Assert(mUnifiers[kvp.Key] != null);
            }            
        }

        private CatKind ResolveKind(CatKind k)
        {
            if (k.IsKindVar())
            {
                if (mUnifiers.ContainsKey(k.ToString()))
                    return mUnifiers[k.ToString()];
                else
                    return k;
            }
            else if (k is CatTypeVector)
            {
                CatTypeVector v = k as CatTypeVector;
                CatTypeVector ret = new CatTypeVector();
                foreach (CatKind tmp in v.GetKinds())
                    ret.PushKind(ResolveKind(tmp));
                return ret;
            }
            else if (k is CatSelfType)
            {
                return k;
            }
            else if (k is CatFxnType)
            {
                CatFxnType ft = k as CatFxnType;
                CatTypeVector cons = ResolveKind(ft.GetCons()) as CatTypeVector;
                CatTypeVector prod = ResolveKind(ft.GetProd()) as CatTypeVector;
                CatFxnType ret = new CatFxnType(cons, prod, ft.HasSideEffects());
                return ret;
            }
            else
            {
                return k;
            }
        }

        private void ResolveUnifiers()
        {
            string[] a = new string[mUnifiers.Count];
            mUnifiers.Keys.CopyTo(a, 0);
            foreach (string s in a)
            {
                mUnifiers[s] = ResolveKind(mUnifiers[s]);
            }
        }

        public Dictionary<string, CatKind> GetResolvedUnifiers()
        {
            CreateUnifiers();
            ResolveUnifiers();
            return mUnifiers;
        }

        public void Clear()
        {
            mConstraintListList.Clear();
            mConstraints.Clear();
            mUnifiers.Clear();
        }
    }

}