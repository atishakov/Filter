using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Linq;

namespace MyFilter
{

     // Параметризованный класс фильтра	
    public class Filter<T> : IFilter
    {	        
	    // Конструктрор
        public Filter(bool isNotRoot)
        {
            IsNotRoot = isNotRoot;

            // "And" by default
            AndOrIdx = 0;
            CmpExpressions = new List<CmpExpression>();
            CmpExpressions.Clear();
            Properties = typeof(T).GetProperties().ToList<PropertyInfo>();                
        }

        public Filter(bool isNotRoot, XElement xml)
        {
            IsNotRoot = isNotRoot;            

            // "And" by default
            //AndOrIdx = 0;
            
            CmpExpressions = new List<CmpExpression>();
            CmpExpressions.Clear();
            int i = 0;

            Properties = typeof(T).GetProperties().ToList<PropertyInfo>();
            AndOrIdx = Convert.ToInt32(xml.Attribute("AndOr").Value);
            foreach (XElement xElement in xml.Elements("CmpExpression"))            
            {
                CmpExpression cmpExpression;
                string name = xElement.Attribute("Name").Value;
                if (xElement.Attribute("IsSimple").Value == "1")
                {
                    PropertyInfo pInfo = Properties.First(p => p.Name == name);
                    cmpExpression = new CmpExpression()
                    {
                        ID = i,
                        BinaryExprList = (pInfo.PropertyType == typeof(string)) ? this.BinaryExprListForStrings : this.BinaryExprList,
                        IsSimpleExpression = true,                        
                        Name = name,
                        Not = (xElement.Attribute("Not").Value == "1") ? (true) : (false),
                        ExpType = Convert.ToInt32(xElement.Attribute("ExpType").Value),
                        CmpValue = xElement.Attribute("CmpValue").Value,
                        PropertyType = pInfo.PropertyType,
                        BinaryExpressions = this.BinaryExpressions,
                        SelectedExprIndex = Convert.ToInt32(xElement.Attribute("ExpIdx").Value)
                    };
                }
                else
                {
                    Filter<T> filter = new Filter<T>(true, xElement);                    
                    filter.Parent = this;                                   
                    cmpExpression = new CmpExpression()
                    {   
                        ID = i,
                        ExpType = 1,
                        IsSimpleExpression = false,
                        Name = name,
                        Filter = filter
                    };                    
                }

                CmpExpressions.Add(cmpExpression);
                i++;
            }
        }

        public bool IsNotRoot
        { set; get; }

        public int AndOrIdx
        { set; get; }

        public List<PropertyInfo> Properties
        { set; get; }

        public List<CmpExpression> CmpExpressions
        { set; get; }

        public List<string> BinaryExprList = new List<string>() { "=", "!=", ">", ">=", "<", "<=" };

        public List<string> BinaryExprListForStrings = new List<string>() { "=", "!=", ">", ">=", "<", "<=", "Contains", "Begins with", "Ends with" };

        public IFilter Parent
        { set; get; }

        public CmpExpression ParentCmpExpression
        { set; get; }

        private List<Func<Expression, Expression, BinaryExpression>> AndOrExprsns = new List<Func<Expression, Expression, BinaryExpression>>()
        {
            Expression.And,
            Expression.Or
        };

        private List<Func<Expression, Expression, BinaryExpression>> BinaryExpressions =
            new List<Func<Expression, Expression, BinaryExpression>>()
            {
            Expression.Equal,
            Expression.NotEqual,
            Expression.GreaterThan,
            Expression.GreaterThanOrEqual,            
            Expression.LessThan,
            Expression.LessThanOrEqual
            };

        private string[] AndOr = new string[] { " AND ", " OR " };

        private int GenPropID()
        {
            int i = CmpExpressions.Count;
            while (CmpExpressions.Exists(cp => cp.ID == i))
                i++;
            return i;
        }
        
        /// <summary>
        /// Строим выражение
        /// </summary>
        /// <returns></returns>
        public Expression<Func<T, bool>> BuildSubExpression()
        {         
            ParameterExpression obj = Expression.Parameter(typeof(T), "T");
            Expression<Func<T, bool>> exp = Expression.Lambda<Func<T, bool>>
            (Expression.Constant(true, typeof(bool)), new ParameterExpression[] { obj });
            bool isEmpty = true;
            Expression<Func<T, bool>> exp1;
            foreach (CmpExpression cp in CmpExpressions)
            {                
                if ((cp.IsCompoundExpression)||(cp.CmpValue.Length > 0))
                {
                    if (cp.IsCompoundExpression)
                        exp1 = ((Filter<T>) cp.Filter).BuildSubExpression();
                    else
                    {
                        if (cp.PropertyType == typeof(string))
                            exp1 = CmpStrings(obj, cp);
                        else
                            if (cp.PropertyType == typeof(bool))
                                exp1 = CmpBools(obj, cp);
                            else
                                exp1 = CmpNumbers(obj, cp);
                    } 
                    
                    if (cp.Not)
                        exp1 = Expression.Lambda<Func<T, bool>>(Expression.Not(Expression.Invoke(exp1, new[] { obj })), new ParameterExpression[] { obj });

                    if (isEmpty)
                    {
                        exp = exp1;
                        isEmpty = false;
                    }
                    else
                    {                        
                        exp = Expression.Lambda<Func<T, bool>>
                        (AndOrExprsns[AndOrIdx](
                        Expression.Invoke(exp, new[] { obj })
                        ,Expression.Invoke(exp1, new[] {obj})),
                        new ParameterExpression[] { obj });                        
                    }
                }
            }
            return exp;
        }

        public Expression<Func<T, bool>> BuildExpression()
        {
            Filter<T> filter = this;
            while (filter.Parent != null)
                filter = (Filter<T>)filter.Parent;
            return filter.BuildSubExpression();
        }

        /// <summary>
        /// Создает выражение для сравнения числового свойства с константой или другим свойством
        /// </summary>
        /// <param name="expIdx">Номер выражения сравнения</param>
        /// <param name="obj">Параметр выражения (Экземпляр класса T)</param>
        /// <param name="cp"></param>
        /// <returns></returns>
        Expression<Func<T, bool>> CmpNumbers(ParameterExpression obj, CmpExpression cp)
        {
            int expIdx = cp.SelectedExprIndex;
            string PropertyName = "";
            bool cmpProperties = false;
	        // Другое свойство класса задается в выражении сравнения в квадратных скобках
            if ((cp.CmpValue[0] == '[') && (cp.CmpValue[cp.CmpValue.Length - 1] == ']'))
            {
                PropertyName = cp.CmpValue.Substring(1, cp.CmpValue.Length - 2);
                cmpProperties = Properties.Exists(p => (p.Name == PropertyName) && (p.PropertyType == cp.PropertyType));
            };

	        // Если в выражении сравнения другое свойство класса T	
            if (cmpProperties)
                return Expression.Lambda<Func<T, bool>>
                                (cp.BinaryExpressions[expIdx](
                                    Expression.Property(obj, cp.Name),
                                    Expression.Property(obj, PropertyName)),
                                new ParameterExpression[] { obj });
            // Если в выражении константа
            else                            
                return Expression.Lambda<Func<T, bool>>
                                (cp.BinaryExpressions[expIdx](
                                    Expression.Property(obj, cp.Name),
                                    Expression.Constant(Convert.ChangeType(cp.CmpValue, cp.PropertyType), cp.PropertyType)),
                                new ParameterExpression[] { obj });            
        }

        Expression<Func<T, bool>> CmpBools(ParameterExpression obj, CmpExpression cp)
        {
            int expIdx = 1 - Convert.ToInt32(cp.CmpValue);
            return Expression.Lambda<Func<T, bool>>
                (cp.BinaryExpressions[expIdx](
                    Expression.Property(obj, cp.Name),
                    Expression.Constant(true, typeof(bool))),
                new ParameterExpression[] { obj });
        }

	    // Сравнение строк
        Expression<Func<T, bool>> CmpStrings(ParameterExpression obj, CmpExpression cp)
        {
            int expIdx = cp.SelectedExprIndex;
            if (expIdx < 6)
            {
                return Expression.Lambda<Func<T, bool>>
                                (cp.BinaryExpressions[expIdx](
                                    Expression.Call(Expression.Property(obj, cp.Name), typeof(String).GetMethod("CompareTo", new Type[] { typeof(string) }),
                                        new Expression[] { Expression.Constant(Convert.ChangeType(cp.CmpValue, cp.PropertyType), cp.PropertyType) }),
                                    Expression.Constant(0))
                                    ,
                                new ParameterExpression[] { obj });
            }
            else
            {
                string expName = (expIdx == 6) ? "Contains" : ((expIdx == 7) ? "StartsWith" : "EndsWith");
                return Expression.Lambda<Func<T, bool>>
                            (Expression.Equal(
                                Expression.Call(Expression.Property(obj, cp.Name), typeof(String).GetMethod(expName, new Type[] { typeof(string) }),
                                    new Expression[] { Expression.Constant(Convert.ChangeType(cp.CmpValue, cp.PropertyType), cp.PropertyType) }),
                                Expression.Constant(true))
                                ,
                            new ParameterExpression[] { obj });
            }
        }
        
        public void AddSimpleExpression(int pID)
        {
            int idx = CmpExpressions.FindIndex(p => p.ID == pID);
            if (idx < 0)
                idx = 0;
            CmpExpressions.Insert(idx + 1,
                new CmpExpression
                {
                    ID = GenPropID(),
                    ExpType = CmpExpressions[idx].ExpType,
                    IsSimpleExpression = true,
                    Name = CmpExpressions[idx].Name,
                    BinaryExprList = CmpExpressions[idx].BinaryExprList,
                    BinaryExpressions = CmpExpressions[idx].BinaryExpressions,
                    PropertyType = CmpExpressions[idx].PropertyType,
                    CmpValue = CmpExpressions[idx].CmpValue,
                    SelectedExprIndex = CmpExpressions[idx].SelectedExprIndex
                }
            );  
        }

        public void AddSimpleExpression(PropertyInfo pInfo)
        {
            if (pInfo == null)
                return;
            CmpExpressions.Add(
                new CmpExpression
                {
                    ID = GenPropID(),
                    IsSimpleExpression = true,
                    ExpType = (pInfo.PropertyType == typeof(bool)) ? 2 : 4,
                    Name = pInfo.Name,                   
                    CmpValue = "",
                    PropertyType = pInfo.PropertyType,
                    BinaryExpressions = this.BinaryExpressions,
                    BinaryExprList = (pInfo.PropertyType == typeof(string)) ? this.BinaryExprListForStrings : this.BinaryExprList,
                    SelectedExprIndex = 0                    
                }
            );
        }

        public IFilter GetSubFilter(int pID)
        {
            int idx = CmpExpressions.FindIndex(p =>((!p.IsSimpleExpression) && (p.ID == pID)));            
            if (idx < 0)
                return this;
            return CmpExpressions[idx].Filter;
        }

        public void AddCompoundExpression()
        {
            CmpExpression ccp = new CmpExpression()
            {
                ID = GenPropID(),
                ExpType = 1,
                IsSimpleExpression = false,               
                Filter = new Filter<T>(true)
            };
            ccp.Filter.Parent = (IFilter)this;
            CmpExpressions.Add(ccp);
        }

        public void AddCompoundExpression(int idx, IFilter filter)
        {
            CmpExpression ccp = new CmpExpression()
            {
                ID = GenPropID(),
                ExpType = 1,
                IsSimpleExpression = false,                
                Filter = filter
            };
            ccp.Filter.Parent = this;
            CmpExpressions.Insert(idx, ccp);
        }

        public void RemoveExpression(int pID)
        {
            int idx = CmpExpressions.FindIndex(p => p.ID == pID);
            if (idx >= 0)            
                CmpExpressions.RemoveAt(idx);                        
        }

        public IFilter PutIntoBrackets()
        {
            Filter<T> filter = new Filter<T>(this.IsNotRoot);
            filter.Parent = this.Parent;
            this.Parent = filter;
            this.IsNotRoot = true;
            if (this.ParentCmpExpression!=null)
                this.ParentCmpExpression.Filter = filter;
            filter.AddCompoundExpression(0, this);
            return filter; 
        }

        public XElement GetXML(string name)
        {
            XElement xml = new XElement("CmpExpression", new XAttribute("IsSimple", "0"), new XAttribute("AndOr", AndOrIdx.ToString())
                , new XAttribute("Name", name));
            XElement exXml;
            foreach (CmpExpression cp in CmpExpressions)
            {
                if ((cp.IsCompoundExpression) || (cp.CmpValue.Length > 0))
                {
                    if (cp.IsCompoundExpression)
                        exXml = cp.Filter.GetXML(cp.Name);
                    else
                    {                        
                        exXml = new XElement("CmpExpression"                            
                            , new XAttribute("IsSimple", "1")
                            , new XAttribute("Not", (cp.Not ? "1" : "0"))
                            , new XAttribute("Name", cp.Name)
                            , new XAttribute("ExpIdx", cp.SelectedExprIndex.ToString())
                            , new XAttribute("ExpType", cp.ExpType.ToString())
                            , new XAttribute("CmpValue", cp.CmpValue)
                            , new XAttribute("PropertyType", cp.PropertyType.ToString()));
                    }
                    xml.Add(exXml);
                }
            }
            return xml;
        }

        public XElement GetXMLRoot()
        {
            Filter<T> filter = this;
            while (filter.Parent != null)
                filter = (Filter<T>)filter.Parent;
            return filter.GetXML("Root");
        }

        public string SubQueryText()
        {
            string subQueryText = "";
            CmpExpression cp;
            int j = 0;
            while ((j<CmpExpressions.Count)&&(!CmpExpressions[j].IsCompoundExpression)&&(CmpExpressions[j].CmpValue.Length == 0))
                j++;
                    
            if (j<CmpExpressions.Count)
            {
                cp = CmpExpressions[j];
                if (cp.IsCompoundExpression)
                    subQueryText += (cp.Not ? " NOT " : "") + "(" + cp.Filter.SubQueryText() + ")";
                else
                    if (cp.CmpValue.Length > 0)
                    {
                        string qs = (cp.SelectedExprIndex > 5) ? "'" : "";
                        subQueryText += (cp.Not ? " NOT " : "") + "(" + cp.Name + " " + BinaryExprListForStrings[cp.SelectedExprIndex] + " " + qs + cp.CmpValue + qs + ")";
                    }                

                for (int i = j+1; i < CmpExpressions.Count; i++)
                {
                    cp = CmpExpressions[i];

                    if (cp.IsCompoundExpression)
                        subQueryText += AndOr[AndOrIdx] + (cp.Not ? " NOT " : "") + "(" + cp.Filter.SubQueryText() + ")";
                    else
                        if (cp.CmpValue.Length > 0)                    
                        {
                            string qs = (cp.SelectedExprIndex > 5) ? "'" : "";
                            subQueryText += AndOr[AndOrIdx] + (cp.Not ? " NOT " : "") + "(" + cp.Name + " " + 
                                BinaryExprListForStrings[cp.SelectedExprIndex] + " " + qs + cp.CmpValue + qs + ")";
                        }
                    }
                }            
            return subQueryText;
        }

        public string QueryText()
        {
            Filter<T> filter = this;
            while (filter.Parent != null)
                filter = (Filter<T>)filter.Parent;
            return filter.SubQueryText();
        }
    }
    
    public class CmpExpression
    {
        public int ID
        { set; get; }

        public bool IsCompoundExpression
        {
            get
            {
                return !IsSimpleExpression;
            }
        }

        public int ExpType
        { set; get; }

        public bool Not
        { set; get; }

        private bool _IsSimpleExpression;
        public bool IsSimpleExpression
        { set { if (!value) ExpType = (ExpType | 1); _IsSimpleExpression = value; } get { return _IsSimpleExpression; } }

        private IFilter _Filter;
        public IFilter Filter
        { set { value.ParentCmpExpression = this; _Filter = value; } get { return _Filter; } }
        string _Name;
        public string Name
        {
            set { _Name = value; }
            get
            {
                if (IsSimpleExpression)
                    return _Name;
                else return Filter.SubQueryText();
            }
        }
        public string CmpValue
        { set; get; }
        
        public Type PropertyType
        { set ;  get ;}
        public List<Func<Expression, Expression, BinaryExpression>> BinaryExpressions
        { set; get; }
        public List<string> BinaryExprList
        { set; get; }
        public int SelectedExprIndex 
        { set; get; }
    }

    public interface IFilter
    {
        int AndOrIdx
        { set; get; }
        List<PropertyInfo> Properties
        { set; get; }
        IFilter Parent
        { set; get; }
        List<CmpExpression> CmpExpressions
        { set; get; }
        CmpExpression ParentCmpExpression
        { set; get; }
        void AddCompoundExpression();
        void AddCompoundExpression(int idx, IFilter filter);
        void AddSimpleExpression(PropertyInfo pInfo);
        void AddSimpleExpression(int pID);
        void RemoveExpression(int pID);        
        IFilter PutIntoBrackets();
        IFilter GetSubFilter(int pID);
        string QueryText();
        string SubQueryText();        
        XElement GetXML(string name);
        XElement GetXMLRoot();
    }

    interface IFilterControl<T>
    {        
        Filter<T> Filter { get; }
    }    
}
