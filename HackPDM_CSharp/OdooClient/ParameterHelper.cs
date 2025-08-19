using HackPDM.ClientUtils;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace HackPDM
{
    //public class ParameterHelper 
    //{
    //    // empty static arraylist
    //    private static ArrayList empty = [];
    //    public static ArrayList Empty
    //    {
    //        get => empty;
    //    }
    //    ArrayList combined;
    //    ArrayList parameters = [];
    //    ArrayList subParameters = [];
    //    ArrayList domainOperators = [];
    //    ArrayList fields = [];

    //    public void Add(string fieldName, Operators op, object value) => subParameters.Add(new ArrayList() { fieldName, op.OperatorConversion(), value});
    //    public void Add(string fieldName, Operators op, params object[] values) => subParameters.Add(new ArrayList() { fieldName, op.OperatorConversion(), values.ToArrayList()});
    //    //public void Add(string fieldName, Operators op, IEnumerable values) => parameters.Add(new ArrayList() { fieldName, op.OperatorConversion(), values.ToArrayList() });
    //    public void Add(string fieldName, Operators op, ArrayList values) => subParameters.Add(new ArrayList() { fieldName, op.OperatorConversion(), values });
    //    public void AddFields(params object[] fieldNames) => fields.AddRange(fieldNames);
    //    public void AddDomainOperator(DomainOperators op)
    //    {
    //        domainOperators.Add(op.OperatorConversion());
    //        parameters.Add(subParameters);
    //        subParameters = [];
    //    }

    //    public ParameterHelper AddR(string fieldName, Operators op, object value)
    //    {
    //        this.Add(fieldName, op, value);
    //        return this;
    //    }
    //    public ParameterHelper AddFieldsR(params object[] fieldNames)
    //    {
    //        this.AddFields(fieldNames);
    //        return this;
    //    }
    //    public ParameterHelper AddDomainOperatorR(DomainOperators op)
    //    {
    //        this.AddDomainOperator(op);
    //        return this;
    //    }
        
    //    public void Clear()
    //    {
    //        this.combined = [];
    //        this.parameters = [];
    //        this.subParameters = [];
    //        this.fields = [];
    //        this.domainOperators = [];
    //    }

    //    public ArrayList Build()
    //    {
    //        return BuildInternal(true);
    //    }
    //    private ArrayList BuildInternal(bool withFields)
    //    {

    //        ArrayList domain = [];

    //        for (int i = 0; i < domainOperators.Count; i++)
    //        {
    //            domain.Add(domainOperators[i]);
    //        }
    //        for (int i = 0; i < parameters.Count; i++)
    //        {
    //            domain.Add(parameters[i]);
    //        }
    //        domain.Add(subParameters);
    //        // just a domain operator that can be added as a string inside the arraylist
    //        //for (int i = 0; i < parameters.Count; i++)
    //        //{
    //        //    ArrayList pList = (ArrayList)parameters[i];
    //        //    for (int j = 0; j < pList.Count; j++)
    //        //    {
    //        //        ArrayList pSubList = (ArrayList)pList[j];
    //        //        domain.Add(new ArrayList() { pSubList[0], pSubList[1], pSubList[2] });
    //        //    }
    //        //}
    //        //for (int j = 0; j < subParameters.Count; j++)
    //        //{
    //        //    ArrayList pSubList = (ArrayList)subParameters[j];
    //        //    if ((j + 2) % 2 == 0)
    //        //    {
    //        //        domain.Add("&");
    //        //    }
    //        //    domain.Add(new ArrayList() { pSubList[0], pSubList[1], pSubList[2] });
    //        //}
    //        //foreach (ArrayList par in parameters)
    //        //{
    //        //    if (par is ArrayList list)
    //        //    {
    //        //        // should be a string array with 3 items for the fieldName, operator, value
    //        //        domain.Add(new ArrayList() { list[0], list[1], list[2] });
    //        //    }
    //        //}
    //        if (withFields)
    //        {
    //            if (fields.Count > 0)
    //            {
    //                combined = [domain, fields];
    //                return combined;
    //            }
    //        }
    //        return combined = [domain];
    //    }
    //    public static ArrayList TupledBuild(DomainOperators op, params ArrayList[] allrecords)
    //    {
    //        ArrayList domain = [];

    //        if (allrecords == null || allrecords.Length == 0)
    //            return null;

    //        if (allrecords.Length == 1)
    //        {
    //            domain.Add(allrecords[0]);
    //            return domain;
    //        }
    //        domain = RecurseTupleBuild(0, DomainOperators.Or, allrecords);
    //        return domain;
    //    }
    //    private static ArrayList RecurseTupleBuild(int index, in DomainOperators op, in ArrayList[] allrecords)
    //    {
    //        ArrayList nextList = [allrecords[index]];
            
    //        if (index < allrecords.Length - 2)
    //        {
    //            ArrayList list = RecurseTupleBuild(index + 1, op, allrecords);
    //            nextList.Add(list);
    //            return list;
    //        }
    //        else
    //        {
    //            nextList.Add(allrecords[index + 1]);
    //            return nextList;
    //        }
    //    }
    //}
    public static class OpExtension
    {
        public static string OperatorConversion(this Operators op)
        {
            switch (op)
            {
                case Operators.Equal: return "=";
                case Operators.NotEqual: return "!=";
                case Operators.GreaterThan: return ">";
                case Operators.GreaterThanOrEqual: return ">=";
                case Operators.LessThan: return "<";
                case Operators.LessThanOrEqual: return "<=";
                case Operators.Unset: return "=?";
                case Operators.Like: return "like";
                case Operators.LikeEqual: return "=like";
                case Operators.NotLike: return "not like";
                case Operators.ILike: return "ilike";
                case Operators.NotILike: return "not ilike";
                case Operators.ILikeEqual: return "=ilike";
                case Operators.In: return "in";
                case Operators.NotIn: return "not in";
                case Operators.ChildOf: return "child_of";
                case Operators.ParentOf: return "parent_of";
                default: throw new ArgumentException();
            }
        }
        public static string OperatorConversion(this DomainOperators op)
        {
            switch (op)
            {
                case DomainOperators.And: return "&";
                case DomainOperators.Or: return "|";
                case DomainOperators.Not: return "!";
                default: throw new ArgumentException();
            }
        }
    }

}