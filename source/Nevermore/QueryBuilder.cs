﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.RegularExpressions;
using Nevermore.AST;

namespace Nevermore
{
    public class QueryBuilder<TRecord, TSelectBuilder> : IOrderedQueryBuilder<TRecord> where TSelectBuilder: ISelectBuilder where TRecord : class
    {
        readonly TSelectBuilder selectBuilder;
        readonly IRelationalTransaction transaction;
        readonly ITableAliasGenerator tableAliasGenerator;
        readonly IUniqueParameterNameGenerator uniqueParameterNameGenerator;
        readonly CommandParameterValues paramValues;
        readonly Parameters @params;
        readonly ParameterDefaults paramDefaults;

        public QueryBuilder(TSelectBuilder selectBuilder, 
            IRelationalTransaction transaction,
            ITableAliasGenerator tableAliasGenerator, 
            IUniqueParameterNameGenerator uniqueParameterNameGenerator, 
            CommandParameterValues paramValues, 
            Parameters @params, 
            ParameterDefaults paramDefaults)
        {
            this.selectBuilder = selectBuilder;
            this.transaction = transaction;
            this.tableAliasGenerator = tableAliasGenerator;
            this.uniqueParameterNameGenerator = uniqueParameterNameGenerator;
            this.paramValues = paramValues;
            this.@params = @params;
            this.paramDefaults = paramDefaults;
        }

        public IQueryBuilder<TRecord> Where(string whereClause)
        {
            if (!String.IsNullOrWhiteSpace(whereClause))
            {
                var whereClauseNormalised = Regex.Replace(whereClause, @"@\w+", m => new Parameter(m.Value).ParameterName);
                selectBuilder.AddWhere(whereClauseNormalised);
            }
            return this;
        }

        public IUnaryParameterQueryBuilder<TRecord> WhereParameterised(string fieldName, UnarySqlOperand operand, Parameter parameter)
        {
            var uniqueParameter = new UniqueParameter(uniqueParameterNameGenerator, parameter);
            selectBuilder.AddWhere(new UnaryWhereParameter(fieldName, operand, uniqueParameter));
            return new UnaryParameterQueryBuilder<TRecord>(Parameter(uniqueParameter), uniqueParameter);
        }

        public IBinaryParametersQueryBuilder<TRecord> WhereParameterised(string fieldName, BinarySqlOperand operand,
            Parameter startValueParameter, Parameter endValueParameter)
        {
            var uniqueStartParameter = new UniqueParameter(uniqueParameterNameGenerator, startValueParameter);
            var uniqueEndParameter = new UniqueParameter(uniqueParameterNameGenerator, endValueParameter);
            selectBuilder.AddWhere(new BinaryWhereParameter(fieldName, operand, uniqueStartParameter, uniqueEndParameter));
            return new BinaryParametersQueryBuilder<TRecord>(Parameter(uniqueStartParameter).Parameter(uniqueEndParameter), uniqueStartParameter, uniqueEndParameter);
        }

        public IArrayParametersQueryBuilder<TRecord> WhereParameterised(string fieldName, ArraySqlOperand operand,
            IEnumerable<Parameter> parameterNames)
        {
            var parameterNamesList = parameterNames.Select(p => new UniqueParameter(uniqueParameterNameGenerator, p)).ToList();
            if (!parameterNamesList.Any())
            {
                return new ArrayParametersQueryBuilder<TRecord>(AddAlwaysFalseWhere(), parameterNamesList);
            }
            selectBuilder.AddWhere(new ArrayWhereParameter(fieldName, operand, parameterNamesList));
            IQueryBuilder<TRecord> builder = this;
            return new ArrayParametersQueryBuilder<TRecord>(parameterNamesList.Aggregate(builder, (b, p) => b.Parameter(p)), parameterNamesList);
        }

        IQueryBuilder<TRecord> AddAlwaysFalseWhere()
        {
            return Where("0 = 1");
        }

        public IQueryBuilder<TRecord> AllColumns()
        {
            selectBuilder.AddDefaultColumnSelection();
            return this;
        }

        public IQueryBuilder<TRecord> CalculatedColumn(string expression, string columnAlias)
        {
            selectBuilder.AddColumnSelection(new AliasedColumn(new CalculatedColumn(expression), columnAlias));
            return this;
        }

        public IQueryBuilder<TNewRecord> AsType<TNewRecord>() where TNewRecord : class
        {
            return new QueryBuilder<TNewRecord, TSelectBuilder>(selectBuilder, transaction, tableAliasGenerator, uniqueParameterNameGenerator, ParameterValues, Parameters, ParameterDefaults);
        }

        public IQueryBuilder<TRecord> AddRowNumberColumn(string columnAlias)
        {
            return AddRowNumberColumn(columnAlias, new string[0]);
        }

        public IQueryBuilder<TRecord> AddRowNumberColumn(string columnAlias, params string[] partitionByColumns)
        {
            selectBuilder.AddRowNumberColumn(columnAlias, partitionByColumns.Select(c => new Column(c)).ToList());
            return this;
        }

        public IQueryBuilder<TRecord> AddRowNumberColumn(string columnAlias, params ColumnFromTable[] partitionByColumns)
        {
            selectBuilder.AddRowNumberColumn(columnAlias, partitionByColumns.Select(c => new TableColumn(new Column(c.ColumnName), c.Table)).ToList());
            return this;
        }

        public IQueryBuilder<TRecord> Parameter(Parameter parameter)
        {
            @params.Add(parameter);
            return this;
        }

        public IQueryBuilder<TRecord> ParameterDefault(Parameter parameter, object defaultValue)
        {
            paramDefaults.Add(new ParameterDefault(parameter, defaultValue));
            return this;
        }

        public IQueryBuilder<TRecord> Parameter(Parameter parameter, object value)
        {
            paramValues.Add(parameter.ParameterName, value);
            return this;
        }

        public IJoinSourceQueryBuilder<TRecord> Join(IAliasedSelectSource source, JoinType joinType, CommandParameterValues parameterValues, Parameters parameters, ParameterDefaults parameterDefaults)
        {
            var clonedSelectBuilder = selectBuilder.Clone();
            clonedSelectBuilder.IgnoreDefaultOrderBy();
            var subquery = new SubquerySource(clonedSelectBuilder.GenerateSelect(), tableAliasGenerator.GenerateTableAlias());
            return new JoinSourceQueryBuilder<TRecord>(subquery, 
                joinType, 
                source, 
                transaction, 
                tableAliasGenerator,
                uniqueParameterNameGenerator, 
                new CommandParameterValues(ParameterValues, parameterValues), 
                new Parameters(Parameters, parameters),
                new ParameterDefaults(ParameterDefaults, parameterDefaults));
        }

        public ISubquerySourceBuilder<TRecord> Union(IQueryBuilder<TRecord> queryBuilder)
        {
            var clonedSelectBuilder = selectBuilder.Clone();
            clonedSelectBuilder.IgnoreDefaultOrderBy();
            var unionedSelectBuilder = queryBuilder.GetSelectBuilder();
            unionedSelectBuilder.IgnoreDefaultOrderBy();
            return new SubquerySourceBuilder<TRecord>(new Union(new [] { clonedSelectBuilder.GenerateSelect(), unionedSelectBuilder.GenerateSelect() }), 
                tableAliasGenerator.GenerateTableAlias(),
                transaction, 
                tableAliasGenerator, 
                uniqueParameterNameGenerator, 
                new CommandParameterValues(ParameterValues, queryBuilder.ParameterValues), 
                new Parameters(Parameters, queryBuilder.Parameters), 
                new ParameterDefaults(ParameterDefaults, queryBuilder.ParameterDefaults));
        }

        public ISubquerySourceBuilder<TRecord> Subquery()
        {
            var clonedSelectBuilder = selectBuilder.Clone();
            clonedSelectBuilder.IgnoreDefaultOrderBy();
            return new SubquerySourceBuilder<TRecord>(clonedSelectBuilder.GenerateSelect(), 
                tableAliasGenerator.GenerateTableAlias(), 
                transaction, 
                tableAliasGenerator, 
                uniqueParameterNameGenerator, 
                ParameterValues, 
                Parameters, 
                ParameterDefaults);
        }

        SubquerySelectBuilder CreateSubqueryBuilder(ISelectBuilder subquerySelectBuilder)
        {
            subquerySelectBuilder.IgnoreDefaultOrderBy();
            return new SubquerySelectBuilder(new SubquerySource(subquerySelectBuilder.GenerateSelect(), tableAliasGenerator.GenerateTableAlias()));
        }

        public ISelectBuilder GetSelectBuilder()
        {
            return selectBuilder.Clone();
        }

        public IOrderedQueryBuilder<TRecord> OrderBy(string fieldName)
        {
            selectBuilder.AddOrder(fieldName, false);
            return this;
        }


        public IOrderedQueryBuilder<TRecord> OrderByDescending(string fieldName)
        {
            selectBuilder.AddOrder(fieldName, true);
            return this;
        }

        public IQueryBuilder<TRecord> Column(string name)
        {
            selectBuilder.AddColumn(name);
            return this;
        }

        public IQueryBuilder<TRecord> Column(string name, string columnAlias)
        {
            selectBuilder.AddColumn(name, columnAlias);
            return this;
        }

        public IQueryBuilder<TRecord> Column(string name, string columnAlias, string tableAlias)
        {
            selectBuilder.AddColumnSelection(new AliasedColumn(new TableColumn(new Column(name), tableAlias), columnAlias));
            return this;
        }

        [Pure]
        public int Count()
        {
            var clonedSelectBuilder = selectBuilder.Clone();
            clonedSelectBuilder.AddColumnSelection(new SelectCountSource());
            return transaction.ExecuteScalar<int>(clonedSelectBuilder.GenerateSelect().GenerateSql(), paramValues);
        }

        [Pure]
        public bool Any()
        {
            return Count() != 0;
        }

        [Pure]
        public TRecord First()
        {
            return Take(1).FirstOrDefault();
        }

        [Pure]
        public IEnumerable<TRecord> Take(int take)
        {
            var clonedSelectBuilder = selectBuilder.Clone();
            clonedSelectBuilder.AddTop(take);
            return transaction.ExecuteReader<TRecord>(clonedSelectBuilder.GenerateSelect().GenerateSql(), paramValues);
        }

        [Pure]
        public List<TRecord> ToList(int skip, int take)
        {
            const string rowNumberColumnName = "RowNum";
            var minRowParameter = new UniqueParameter(uniqueParameterNameGenerator, new Parameter("_minrow"));
            var maxRowParameter = new UniqueParameter(uniqueParameterNameGenerator, new Parameter("_maxrow"));

            var clonedSelectBuilder = selectBuilder.Clone();

            clonedSelectBuilder.AddDefaultColumnSelection();
            clonedSelectBuilder.AddRowNumberColumn(rowNumberColumnName, new List<Column>());

            var subqueryBuilder = CreateSubqueryBuilder(clonedSelectBuilder);
            subqueryBuilder.AddWhere(new UnaryWhereParameter(rowNumberColumnName, UnarySqlOperand.GreaterThanOrEqual, minRowParameter));
            subqueryBuilder.AddWhere(new UnaryWhereParameter(rowNumberColumnName, UnarySqlOperand.LessThanOrEqual, maxRowParameter));
            subqueryBuilder.AddOrder("RowNum", false);
            
            var parmeterValues = new CommandParameterValues(paramValues)
            {
                {minRowParameter.ParameterName, skip + 1},
                {maxRowParameter.ParameterName, take + skip}
            };

            return transaction.ExecuteReader<TRecord>(subqueryBuilder.GenerateSelect().GenerateSql(), parmeterValues).ToList();
        }

        [Pure]
        public List<TRecord> ToList(int skip, int take, out int totalResults)
        {
            totalResults = Count();
            return ToList(skip, take);
        }

        [Pure]
        public List<TRecord> ToList()
        {
            return Stream().ToList();
        }

        [Pure]
        public IEnumerable<TRecord> Stream()
        {
            return transaction.ExecuteReader<TRecord>(selectBuilder.GenerateSelect().GenerateSql(), paramValues);
        }

        [Pure]
        public IDictionary<string, TRecord> ToDictionary(Func<TRecord, string> keySelector)
        {
            return Stream().ToDictionary(keySelector, StringComparer.OrdinalIgnoreCase);
        }

        public Parameters Parameters => new Parameters(@params);
        public ParameterDefaults ParameterDefaults => new ParameterDefaults(paramDefaults);
        public CommandParameterValues ParameterValues => new CommandParameterValues(paramValues);

        [Pure]
        public string DebugViewRawQuery()
        {
            return selectBuilder.GenerateSelect().GenerateSql();
        }
    }
}