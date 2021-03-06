using System.Collections.Generic;
using Nevermore.AST;

namespace Nevermore
{
    public interface ISelectBuilder
    {
        void AddTop(int top);

        void AddOrder(string fieldName, bool descending);
        void IgnoreDefaultOrderBy();
        void AddWhere(UnaryWhereParameter whereParams);
        void AddWhere(BinaryWhereParameter whereParams);
        void AddWhere(ArrayWhereParameter whereParams);
        void AddWhere(string whereClause);
        void AddColumn(string columnName);
        void AddColumn(string columnName, string columnAlias);
        void AddColumnSelection(ISelectColumns columnSelection);
        void AddRowNumberColumn(string alias, IReadOnlyList<Column> partitionBys);
        void AddRowNumberColumn(string alias, IReadOnlyList<TableColumn> partitionBys);
        void AddDefaultColumnSelection();
        ISelect GenerateSelect();

        ISelectBuilder Clone();
    }
}