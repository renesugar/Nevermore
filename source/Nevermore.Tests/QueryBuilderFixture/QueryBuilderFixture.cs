using System;
using System.Collections.Generic;
using Assent;
using FluentAssertions;
using Nevermore.AST;
using Nevermore.Contracts;
using NSubstitute;
using Xunit;

namespace Nevermore.Tests.QueryBuilderFixture
{
    public class QueryBuilderFixture
    {
        readonly ITableAliasGenerator tableAliasGenerator = new TableAliasGenerator();
        readonly IUniqueParameterNameGenerator uniqueParameterNameGenerator = new UniqueParameterNameGenerator();
        readonly IRelationalTransaction transaction = Substitute.For<IRelationalTransaction>();
        
        ITableSourceQueryBuilder<TDocument> CreateQueryBuilder<TDocument>(string tableName) where TDocument : class
        {
            return new TableSourceQueryBuilder<TDocument>(tableName, transaction, tableAliasGenerator, uniqueParameterNameGenerator, new CommandParameterValues(), new Parameters(), new ParameterDefaults());
        }

        [Fact]
        public void ShouldGenerateSelect()
        {
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .Where("[Price] > 5")
                .OrderBy("Name")
                .DebugViewRawQuery();

            const string expected = @"SELECT *
FROM dbo.[Orders]
WHERE ([Price] > 5)
ORDER BY [Name]";

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ShouldGenerateSelectNoOrder()
        {
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .Where("[Price] > 5")
                .DebugViewRawQuery();

            const string expected = @"SELECT *
FROM dbo.[Orders]
WHERE ([Price] > 5)
ORDER BY [Id]";

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ShouldGenerateSelectForQueryBuilder()
        {
            var actual = CreateQueryBuilder<IDocument>("Orders")
             .Where("[Price] > 5")
             .DebugViewRawQuery();

            const string expected = @"SELECT *
FROM dbo.[Orders]
WHERE ([Price] > 5)
ORDER BY [Id]";

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ShouldGenerateSelectForJoin()
        {
            var leftQueryBuilder = CreateQueryBuilder<IDocument>("Orders")
                .Where("[Price] > 5");
            var rightQueryBuilder = CreateQueryBuilder<IDocument>("Customers");

            var actual = leftQueryBuilder
                .InnerJoin(rightQueryBuilder)
                .On("CustomerId", JoinOperand.Equal, "Id")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateSelectForMultipleJoins()
        {

            var leftQueryBuilder = CreateQueryBuilder<IDocument>("Orders");
            var join1QueryBuilder = CreateQueryBuilder<IDocument>("Customers");
            var join2QueryBuilder = CreateQueryBuilder<IDocument>("Accounts");

            var actual = leftQueryBuilder
                .InnerJoin(join1QueryBuilder).On("CustomerId", JoinOperand.Equal, "Id")
                .InnerJoin(join2QueryBuilder).On("AccountId", JoinOperand.Equal, "Id")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateSelectForMultipleJoinsWithParameter()
        {

            var leftQueryBuilder = CreateQueryBuilder<IDocument>("Orders").Where("CustomerId", UnarySqlOperand.Equal, "customers-1");
            var join1QueryBuilder = CreateQueryBuilder<IDocument>("Customers").Where("Name", UnarySqlOperand.Equal, "Abc");
            var join2QueryBuilder = CreateQueryBuilder<IDocument>("Accounts").Where("Name", UnarySqlOperand.Equal, "CBA");

            var actual = leftQueryBuilder
                .InnerJoin(join1QueryBuilder.Subquery()).On("CustomerId", JoinOperand.Equal, "Id")
                .InnerJoin(join2QueryBuilder.Subquery()).On("AccountId", JoinOperand.Equal, "Id")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateSelectForComplicatedSubqueryJoin()
        {
            var orders = CreateQueryBuilder<IDocument>("Orders");
            var customers = CreateQueryBuilder<IDocument>("Customers")
                .Where("IsActive = 1")
                .OrderBy("Created");

            var accounts = CreateQueryBuilder<IDocument>("Accounts").Hint("WITH (UPDLOCK)");

            var actual = orders.InnerJoin(customers.Subquery())
                .On("CustomerId", JoinOperand.Equal, "Id")
                .On("Owner", JoinOperand.Equal, "Owner")
                .InnerJoin(accounts.Subquery()).On("AccountId", JoinOperand.Equal, "Id")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldSuppressOrderByWhenGeneratingCount()
        {
            string actual = null;
            transaction.ExecuteScalar<int>(Arg.Do<string>(s => actual = s), Arg.Any<CommandParameterValues>());

            CreateQueryBuilder<IDocument>("Orders")
                .OrderBy("Created")
                .Count();

            var expected = @"SELECT COUNT(*)
FROM dbo.[Orders]";

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ShouldGenerateCount()
        {
            string actual = null;
            transaction.ExecuteScalar<int>(Arg.Do<string>(s => actual = s), Arg.Any<CommandParameterValues>());

            CreateQueryBuilder<IDocument>("Orders")
                .NoLock()
                .Where("[Price] > 5")
                .Count();

            var expected = @"SELECT COUNT(*)
FROM dbo.[Orders] NOLOCK
WHERE ([Price] > 5)";

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ShouldGenerateCountForQueryBuilder()
        {
            string actual = null;
            transaction.ExecuteScalar<int>(Arg.Do<string>(s => actual = s), Arg.Any<CommandParameterValues>());

            CreateQueryBuilder<IDocument>("Orders")
                .NoLock()
                .Where("[Price] > 5")
                .Count();

            const string expected = @"SELECT COUNT(*)
FROM dbo.[Orders] NOLOCK
WHERE ([Price] > 5)";

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ShouldGenerateCountForJoin()
        {
            string actual = null;
            transaction.ExecuteScalar<int>(Arg.Do<string>(s => actual = s), Arg.Any<CommandParameterValues>());

            var leftQueryBuilder = CreateQueryBuilder<IDocument>("Orders")
                .Where("[Price] > 5");
            var rightQueryBuilder = CreateQueryBuilder<IDocument>("Customers");
            leftQueryBuilder.InnerJoin(rightQueryBuilder).On("CustomerId", JoinOperand.Equal, "Id")
                .Count();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGeneratePaginate()
        {
            string actual = null;
            transaction.ExecuteReader<IDocument>(Arg.Do<string>(s => actual = s), Arg.Any<CommandParameterValues>());
            CreateQueryBuilder<IDocument>("Orders")
                .Where("[Price] > 5")
                .OrderBy("Foo")
                .ToList(10, 20);
            
            this.Assent(actual);
        }


        [Fact]
        public void ShouldGeneratePaginateForJoin()
        {
            string actual = null;
            transaction.ExecuteReader<IDocument>(Arg.Do<string>(s => actual = s), Arg.Any<CommandParameterValues>());

            var leftQueryBuilder = CreateQueryBuilder<IDocument>("Orders")
                .Where("[Price] > 5");
            var rightQueryBuilder = CreateQueryBuilder<IDocument>("Customers");
            leftQueryBuilder
                .InnerJoin(rightQueryBuilder).On("CustomerId", JoinOperand.Equal, "Id")
                .ToList(10, 20);

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateTop()
        {
            string actual = null;
            transaction.ExecuteReader<IDocument>(Arg.Do<string>(s => actual = s), Arg.Any<CommandParameterValues>());
            CreateQueryBuilder<IDocument>("Orders")
                .NoLock()
                .Where("[Price] > 5")
                .OrderBy("Id")
                .Take(100);

            var expected = @"SELECT TOP 100 *
FROM dbo.[Orders] NOLOCK
WHERE ([Price] > 5)
ORDER BY [Id]";

            Assert.Equal(expected, actual);
        }


        [Fact]
        public void ShouldGenerateTopForJoin()
        {
            string actual = null;
            transaction.ExecuteReader<IDocument>(Arg.Do<string>(s => actual = s), Arg.Any<CommandParameterValues>());

            var leftQueryBuilder = CreateQueryBuilder<IDocument>("Orders")
                .Where("[Price] > 5");
            var rightQueryBuilder = CreateQueryBuilder<IDocument>("Customers");

            leftQueryBuilder.InnerJoin(rightQueryBuilder).On("CustomerId", JoinOperand.Equal, "Id")
                .Take(100);

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateExpectedLikeParametersForQueryBuilder()
        {
            CommandParameterValues parameterValues = null;
            transaction.ExecuteReader<IDocument>(Arg.Any<string>(), Arg.Do<CommandParameterValues>(pv => parameterValues = pv));

            // We need to make sure parameters like opening square brackets are correctly escaped for LIKE pattern matching in SQL.
            var environment = new
            {
                Id = "Environments-1"
            };
            CreateQueryBuilder<IDocument>("Project")
                .Where("[JSON] LIKE @jsonPatternSquareBracket")
                .LikeParameter("jsonPatternSquareBracket", $"\"AutoDeployReleaseOverrides\":[{{\"EnvironmentId\":\"{environment.Id}\"")
                .Where("[JSON] NOT LIKE @jsonPatternPercentage")
                .LikeParameter("jsonPatternPercentage", $"SomeNonExistantField > 5%")
                .ToList();

            var actualParameter1 = parameterValues["jsonPatternSquareBracket"];
            const string expectedParameter1 = "%\"AutoDeployReleaseOverrides\":[[]{\"EnvironmentId\":\"Environments-1\"%";
            Assert.Equal(actualParameter1, expectedParameter1);

            var actualParameter2 = parameterValues["jsonPatternPercentage"];
            const string expectedParameter2 = "%SomeNonExistantField > 5[%]%";
            Assert.Equal(actualParameter2, expectedParameter2);
        }

        [Fact]
        public void ShouldGenerateExpectedPipedLikeParametersForQueryBuilder()
        {
            CommandParameterValues parameterValues = null;
            transaction.ExecuteReader<IDocument>(Arg.Any<string>(), Arg.Do<CommandParameterValues>(pv => parameterValues = pv));

            CreateQueryBuilder<IDocument>("Project")
                .LikePipedParameter("Name", "Foo|Bar|Baz")
                .ToList();

            Assert.Equal("%|Foo|Bar|Baz|%", parameterValues["Name"]);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereLessThan()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[Todos]
WHERE ([Completed] < @completed)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(2);

            var result = CreateQueryBuilder<Todos>("Todos")
                .Where("[Completed] < @completed")
                .Parameter("completed", 5)
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => int.Parse(cp["completed"].ToString()) == 5));

            Assert.Equal(2, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereLessThanExtension()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[Todos]
WHERE ([Completed] < @completed_0)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(2);

            var result = CreateQueryBuilder<Todos>("Todos")
                .Where("Completed", UnarySqlOperand.LessThan, 5)
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => int.Parse(cp["completed_0"].ToString()) == 5));

            Assert.Equal(2, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereLessThanOrEqual()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[Todos]
WHERE ([Completed] <= @completed)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(10);

            var result = CreateQueryBuilder<Todos>("Todos")
                .Where("[Completed] <= @completed")
                .Parameter("completed", 5)
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => int.Parse(cp["completed"].ToString()) == 5));

            Assert.Equal(10, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereLessThanOrEqualExtension()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[Todos]
WHERE ([Completed] <= @completed_0)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(10);

            var result = CreateQueryBuilder<Todos>("Todos")
                .Where("Completed", UnarySqlOperand.LessThanOrEqual, 5)
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => int.Parse(cp["completed_0"].ToString()) == 5));

            Assert.Equal(10, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereEquals()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[TodoItem]
WHERE ([Title] = @title)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(1);

            var result = CreateQueryBuilder<TodoItem>("TodoItem")
                .Where("[Title] = @title")
                .Parameter("title", "nevermore")
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => cp["title"].ToString() == "nevermore"));

            Assert.Equal(1, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereEqualsExtension()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[TodoItem]
WHERE ([Title] = @title_0)";


            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(1);

            var result = CreateQueryBuilder<TodoItem>("TodoItem")
                .Where("Title", UnarySqlOperand.Equal, "nevermore")
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => cp["title_0"].ToString() == "nevermore"));

            Assert.Equal(1, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereNotEquals()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[TodoItem]
WHERE ([Title] <> @title)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(1);

            var result = CreateQueryBuilder<TodoItem>("TodoItem")
                .Where("[Title] <> @title")
                .Parameter("title", "nevermore")
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => cp["title"].ToString() == "nevermore"));

            Assert.Equal(1, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereNotEqualsExtension()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[TodoItem]
WHERE ([Title] <> @title_0)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(1);

            var result = CreateQueryBuilder<TodoItem>("TodoItem")
                .Where("Title", UnarySqlOperand.NotEqual, "nevermore")
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => cp["title_0"].ToString() == "nevermore"));

            Assert.Equal(1, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereGreaterThan()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[Todos]
WHERE ([Completed] > @completed)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(11);

            var result = CreateQueryBuilder<Todos>("Todos")
                .Where("[Completed] > @completed")
                .Parameter("completed", 5)
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => int.Parse(cp["completed"].ToString()) == 5));

            Assert.Equal(11, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereGreaterThanExtension()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[Todos]
WHERE ([Completed] > @completed_0)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(3);

            var result = CreateQueryBuilder<Todos>("Todos")
                .Where("Completed", UnarySqlOperand.GreaterThan, 5)
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => int.Parse(cp["completed_0"].ToString()) == 5));

            Assert.Equal(3, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereGreaterThanOrEqual()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[Todos]
WHERE ([Completed] >= @completed)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(21);

            var result = CreateQueryBuilder<Todos>("Todos")
                .Where("[Completed] >= @completed")
                .Parameter("completed", 5)
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => int.Parse(cp["completed"].ToString()) == 5));

            Assert.Equal(21, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereGreaterThanOrEqualExtension()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[Todos]
WHERE ([Completed] >= @completed_0)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(21);

            var result = CreateQueryBuilder<Todos>("Todos")
                .Where("Completed", UnarySqlOperand.GreaterThanOrEqual, 5)
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => int.Parse(cp["completed_0"].ToString()) == 5));

            Assert.Equal(21, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereContains()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[TodoItem]
WHERE ([Title] LIKE @title)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(1);

            var result = CreateQueryBuilder<TodoItem>("TodoItem")
                .Where("[Title] LIKE @title")
                .Parameter("title", "%nevermore%")
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => cp["title"].ToString() == "%nevermore%"));

            Assert.Equal(1, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereContainsExtension()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[TodoItem]
WHERE ([Title] LIKE @title_0)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(1);

            var result = CreateQueryBuilder<TodoItem>("TodoItem")
                .Where(t => t.Title.Contains("nevermore"))
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => cp["title_0"].ToString() == "%nevermore%"));

            Assert.Equal(1, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereInUsingWhereString()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[TodoItem]
WHERE ([Title] IN (@nevermore, @octofront))";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(1);

            var result = CreateQueryBuilder<TodoItem>("TodoItem")
                .Where("[Title] IN (@nevermore, @octofront)")
                .Parameter("nevermore", "nevermore")
                .Parameter("octofront", "octofront")
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp =>
                    cp["nevermore"].ToString() == "nevermore"
                    && cp["octofront"].ToString() == "octofront"));

            Assert.Equal(1, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereInUsingWhereArray()
        {
            const string expectedSql = @"SELECT *
FROM dbo.[Project]
WHERE ([State] IN (@state0_0, @state1_1))
ORDER BY [Id]";

            var queryBuilder = CreateQueryBuilder<IDocument>("Project")
                .Where("State", ArraySqlOperand.In, new[] { State.Queued, State.Running });

            queryBuilder.DebugViewRawQuery().Should().Be(expectedSql);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereInUsingWhereList()
        {
            var matches = new List<State>
            {
                State.Queued,
                State.Running
            };
            const string expectedSql = @"SELECT *
FROM dbo.[Project]
WHERE ([State] IN (@state0_0, @state1_1))
ORDER BY [Id]";
            var queryBuilder = CreateQueryBuilder<IDocument>("Project")
                .Where("State", ArraySqlOperand.In, matches);

            queryBuilder.DebugViewRawQuery().Should().Be(expectedSql);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereInUsingEmptyList()
        {
            const string expextedSql = @"SELECT *
FROM dbo.[Project]
WHERE (0 = 1)
ORDER BY [Id]";

            var queryBuilder =
                CreateQueryBuilder<IDocument>("Project").Where("State", ArraySqlOperand.In, new List<State>());

            queryBuilder.DebugViewRawQuery().Should().Be(expextedSql);
        }


        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereInExtension()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[TodoItem]
WHERE ([Title] IN (@title0_0, @title1_1))";


            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(1);

            var result = CreateQueryBuilder<TodoItem>("TodoItem")
                .Where("Title", ArraySqlOperand.In, new[] { "nevermore", "octofront" })
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp =>
                    cp["title0_0"].ToString() == "nevermore"
                    && cp["title1_1"].ToString() == "octofront"));

            Assert.Equal(1, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereBetween()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[Todos]
WHERE ([Completed] BETWEEN @startvalue AND @endvalue)";


            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(1);

            var result = CreateQueryBuilder<Todos>("Todos")
                .Where("[Completed] BETWEEN @startvalue AND @endvalue")
                .Parameter("StartValue", 5)
                .Parameter("EndValue", 10)
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp =>
                    int.Parse(cp["startvalue"].ToString()) == 5 &&
                    int.Parse(cp["endvalue"].ToString()) == 10));

            Assert.Equal(1, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereBetweenExtension()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[Todos]
WHERE ([Completed] BETWEEN @startvalue_0 AND @endvalue_1)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(1);

            var result = CreateQueryBuilder<Todos>("Todos")
                .Where("Completed", BinarySqlOperand.Between, 5, 10)
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp =>
                    int.Parse(cp["startvalue_0"].ToString()) == 5 &&
                    int.Parse(cp["endvalue_1"].ToString()) == 10));

            Assert.Equal(1, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereBetweenOrEqual()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[Todos]
WHERE ([Completed] >= @startvalue AND [Completed] <= @endvalue)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(1);

            var result = CreateQueryBuilder<Todos>("Todos")
                .Where("[Completed] >= @startvalue AND [Completed] <= @endvalue")
                .Parameter("StartValue", 5)
                .Parameter("EndValue", 10)
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp =>
                    int.Parse(cp["startvalue"].ToString()) == 5 &&
                    int.Parse(cp["endvalue"].ToString()) == 10));

            Assert.Equal(1, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForWhereBetweenOrEqualExtension()
        {
            const string expectedSql = @"SELECT COUNT(*)
FROM dbo.[Todos]
WHERE ([Completed] >= @startvalue_0)
AND ([Completed] <= @endvalue_1)";

            transaction.ExecuteScalar<int>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(1);

            var result = CreateQueryBuilder<Todos>("Todos")
                .WhereBetweenOrEqual("Completed", 5, 10)
                .Count();

            transaction.Received(1).ExecuteScalar<int>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp =>
                    int.Parse(cp["startvalue_0"].ToString()) == 5 &&
                    int.Parse(cp["endvalue_1"].ToString()) == 10));

            Assert.Equal(1, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForOrderBy()
        {
            const string expectedSql = @"SELECT TOP 1 *
FROM dbo.[TodoItem]
ORDER BY [Title]";
            var todoItem = new TodoItem { Id = 1, Title = "Complete Nevermore", Completed = false };

            transaction.ExecuteReader<TodoItem>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(new[] { todoItem });

            var result = CreateQueryBuilder<TodoItem>("TodoItem")
                .OrderBy("Title")
                .First();

            transaction.Received(1).ExecuteReader<TodoItem>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => cp.Count == 0));

            Assert.NotNull(result);
            Assert.Equal(todoItem, result);
        }

        [Fact]
        public void ShouldGetCorrectSqlQueryForOrderByDescending()
        {
            const string expectedSql = @"SELECT TOP 1 *
FROM dbo.[TodoItem]
ORDER BY [Title] DESC";
            var todoItem = new TodoItem { Id = 1, Title = "Complete Nevermore", Completed = false };

            transaction.ExecuteReader<TodoItem>(Arg.Is<string>(s => s.Equals(expectedSql)), Arg.Any<CommandParameterValues>())
                .Returns(new[] { todoItem });

            var result = CreateQueryBuilder<TodoItem>("TodoItem")
                .OrderByDescending("Title")
                .First();

            transaction.Received(1).ExecuteReader<TodoItem>(
                Arg.Is(expectedSql),
                Arg.Is<CommandParameterValues>(cp => cp.Count == 0));

            Assert.NotNull(result);
            Assert.Equal(todoItem, result);
        }

        [Fact]
        public void ShouldGenerateAliasForTable()
        {
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .Alias("ORD")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateAliasForSubquery()
        {
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .Subquery()
                .Alias("ORD")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateAliasesForSourcesInJoin()
        {
            var accounts = CreateQueryBuilder<IDocument>("Accounts")
                .Subquery()
                .Alias("ACC");

            var actual = CreateQueryBuilder<IDocument>("Orders")
                .Alias("ORD")
                .InnerJoin(accounts)
                .On("AccountId", JoinOperand.Equal, "Id")
                .Where("Id", UnarySqlOperand.Equal, 1)
                .OrderBy("Name")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateColumnSelection()
        {
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .Column("Foo")
                .Column("Bar")
                .Column("Baz")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateColumnSelectionWithAliases()
        {
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .Column("Foo", "F")
                .Column("Bar", "B")
                .Column("Baz", "B2")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateColumnSelectionWithTableAlias()
        {
            const string ordersTableAlias = "ORD";
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .Alias(ordersTableAlias)
                .Column("Foo", "F", ordersTableAlias)
                .Column("Bar", "B", ordersTableAlias)
                .Column("Baz", "B2", ordersTableAlias)
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateColumnSelectionForJoin()
        {
            var accounts = CreateQueryBuilder<IDocument>("Accounts")
                .Subquery()
                .Alias("ACC");

            var actual = CreateQueryBuilder<IDocument>("Orders")
                .Alias("ORD")
                .InnerJoin(accounts)
                .On("AccountId", JoinOperand.Equal, "Id")
                .Column("Id", "OrderId", "ORD")
                .Column("Id", "AccountId", "Acc")
                .Column("Number") // should come from "ORD"
                .Column("Id", "OrderId2") // should come from "ORD"
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateRowNumber()
        {
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .AddRowNumberColumn("ROWNUM")
                .OrderBy("ROWNUM")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateRowNumberWithOrderBy()
        {
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .OrderBy("Foo")
                .AddRowNumberColumn("ROWNUM")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateRowNumberWithPartitionBy()
        {
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .OrderBy("Foo")
                .AddRowNumberColumn("ROWNUM", "Region", "Area")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateRowNumberWithPartitionByInJoin()
        {
            var account = CreateQueryBuilder<IDocument>("Account");
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .InnerJoin(account)
                .On("AccountId", JoinOperand.Equal, "Id")
                .OrderBy("Foo")
                .AddRowNumberColumn("ROWNUM", "Region", "Area")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateRowNumberWithPartitionByInJoinWithCustomAliases()
        {
            var account = CreateQueryBuilder<IDocument>("Account")
                .Alias("ACC");
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .Alias("ORD")
                .InnerJoin(account)
                .On("AccountId", JoinOperand.Equal, "Id")
                .OrderBy("Foo")
                .AddRowNumberColumn("ROWNUM", new ColumnFromTable("Region", "ACC"), new ColumnFromTable("Area", "ACC"))
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateUnion()
        {
            var account = CreateQueryBuilder<IDocument>("Account")
                .Column("Id", "Id");
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .Column("Id", "Id")
                .Union(account)
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldCollectParameterValuesFromUnion()
        {
            CommandParameterValues parameterValues = null;
            transaction.ExecuteReader<IDocument>(Arg.Any<string>(), Arg.Do<CommandParameterValues>(pv => parameterValues = pv));

            var account = CreateQueryBuilder<IDocument>("Account")
                .Where("Name", UnarySqlOperand.Equal, "ABC")
                .Column("Id", "Id");
            CreateQueryBuilder<IDocument>("Orders")
                .Column("Id", "Id")
                .Union(account)
                .ToList();

            parameterValues.Should().Contain("Name_0", "ABC");
        }

        [Fact]
        public void ShouldCollectParametersAndDefaultsFromUnion()
        {
            var parameter = new Parameter("Name", new NVarCharMax());
            var account = CreateQueryBuilder<IDocument>("Account")
                .WhereParameterised("Name", UnarySqlOperand.Equal, parameter)
                .ParameterDefault("ABC")
                .Column("Id", "Id");
            var query = CreateQueryBuilder<IDocument>("Orders")
                .Column("Id", "Id")
                .Union(account);

            this.Assent(query.AsStoredProcedure("ShouldCollectParametersAndDefaultsFromUnion"));
        }

        [Fact]
        public void ShouldGenerateCalculatedColumn()
        {
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .CalculatedColumn("'CONSTANT'", "MyConstant")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateLeftHashJoin()
        {
            var account = CreateQueryBuilder<IDocument>("Account");
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .LeftHashJoin(account)
                .On("AccountId", JoinOperand.Equal, "Id")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateWithMultipleLeftHashJoinsSubQueryWithPrameter()
        {
            var account = CreateQueryBuilder<IDocument>("Account").Where("Name", UnarySqlOperand.Equal, "Octopus 1");
            var company = CreateQueryBuilder<IDocument>("Company").Where("Name", UnarySqlOperand.Equal, "Octopus 2");
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .LeftHashJoin(account.Subquery())
                .On("AccountId", JoinOperand.Equal, "Id")
                .LeftHashJoin(company.Subquery())
                .On("CompanyId", JoinOperand.Equal, "CompanyId")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateWithMultipleLeftHashJoinsWithTableAndSubQueryWithPrameter()
        {
            var account = CreateQueryBuilder<IDocument>("Account");
            var company = CreateQueryBuilder<IDocument>("Company").Where("Name", UnarySqlOperand.Equal, "Octopus");
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .LeftHashJoin(account)
                .On("AccountId", JoinOperand.Equal, "Id")
                .LeftHashJoin(company.Subquery())
                .On("CompanyId", JoinOperand.Equal, "CompanyId")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        [Fact]
        public void ShouldGenerateMultipleJoinTypes()
        {
            var customers = CreateQueryBuilder<Customer>("Customers")
                .Where(c => c.Name.StartsWith("Bob"));
            var account = CreateQueryBuilder<IDocument>("Account");
            var actual = CreateQueryBuilder<IDocument>("Orders")
                .InnerJoin(customers.AsType<IDocument>().Subquery())
                .On("CustomerId", JoinOperand.Equal, "Id")
                .LeftHashJoin(account)
                .On("AccountId", JoinOperand.Equal, "Id")
                .DebugViewRawQuery();

            this.Assent(actual);
        }

        class Customer
        {
            public string Name { get; set; }
        }

        [Fact]
        public void ShouldGenerateComplexDashboardView()
        {
            const string taskTableAlias = "t";
            const string releaseTableAlias = "r";
            var dashboard = CurrentDeployments()
                .Union(PreviousDeployments())
                .Alias("d")
                .InnerJoin(CreateQueryBuilder<IDocument>("ServerTask").Alias(taskTableAlias))
                .On("TaskId", JoinOperand.Equal, "Id")
                .InnerJoin(CreateQueryBuilder<IDocument>("Release").Alias(releaseTableAlias))
                .On("ReleaseId", JoinOperand.Equal, "Id")
                .Column("Id", "Id")
                .Column("Created", "Created")
                .Column("ProjectId", "ProjectId")
                .Column("EnvironmentId", "EnvironmentId")
                .Column("ReleaseId", "ReleaseId")
                .Column("TaskId", "TaskId")
                .Column("ChannelId", "ChannelId")
                .Column("CurrentOrPrevious", "CurrentOrPrevious")
                .Column("State", "State", taskTableAlias)
                .Column("HasPendingInterruptions", "HasPendingInterruptions", taskTableAlias)
                .Column("HasWarningsOrErrors", "HasWarningsOrErrors", taskTableAlias)
                .Column("ErrorMessage", "ErrorMessage", taskTableAlias)
                .Column("QueueTime", "QueueTime", taskTableAlias)
                .Column("CompletedTime", "CompletedTime", taskTableAlias)
                .Column("Version", "Version", releaseTableAlias)
                .Where("([Rank]=1 AND CurrentOrPrevious='P') OR ([Rank]=1 AND CurrentOrPrevious='C')");

            var actual = dashboard.AsView("Dashboard");

            this.Assent(actual);
        }

        IQueryBuilder<IDocument> CurrentDeployments()
        {
            const string taskTableAlias = "t";
            const string deploymentTableAlias = "d";

            return CreateQueryBuilder<IDocument>("Deployment")
                .Alias(deploymentTableAlias)
                .InnerJoin(CreateQueryBuilder<IDocument>("ServerTask").Alias(taskTableAlias))
                .On("TaskId", JoinOperand.Equal, "Id")
                .CalculatedColumn("'C'", "CurrentOrPrevious")
                .Column("Id", "Id")
                .Column("Created", "Created")
                .Column("ProjectId", "ProjectId")
                .Column("EnvironmentId", "EnvironmentId")
                .Column("ReleaseId", "ReleaseId")
                .Column("TaskId", "TaskId")
                .Column("ChannelId", "ChannelId")
                .OrderByDescending("Created")
                .AddRowNumberColumn("Rank", new ColumnFromTable("EnvironmentId", deploymentTableAlias), new ColumnFromTable("ProjectId", deploymentTableAlias))
                .Where($"NOT (({taskTableAlias}.State = \'Canceled\' OR {taskTableAlias}.State = \'Cancelling\') AND {taskTableAlias}.StartTime IS NULL)");
        }
        
        IQueryBuilder<IDocument> PreviousDeployments()
        {
            const string deploymentTableAlias = "d";
            const string taskTableAlias = "t";
            const string l = "l";
            return CreateQueryBuilder<IDocument>("Deployment")
                .Alias(deploymentTableAlias)
                .InnerJoin(CreateQueryBuilder<IDocument>("ServerTask").Alias(taskTableAlias))
                .On("TaskId", JoinOperand.Equal, "Id")
                .LeftHashJoin(LQuery().Subquery().Alias(l))
                .On("Id", JoinOperand.Equal, "Id")
                .CalculatedColumn("'P'", "CurrentOrPrevious")
                .Column("Id", "Id")
                .Column("Created", "Created")
                .Column("ProjectId", "ProjectId")
                .Column("EnvironemntId", "EnvironmentId")
                .Column("ReleaseId", "ReleaseId")
                .Column("TaskId", "TaskId")
                .Column("ChannelId", "ChannelId")
                .OrderByDescending("Created")
                .AddRowNumberColumn("Rank", new ColumnFromTable("EnvironmentId", deploymentTableAlias),
                    new ColumnFromTable("ProjectId", deploymentTableAlias))
                .Where($"{taskTableAlias}.State = 'Success'")
                .Where($"{l}.Id is null");
        }

        IQueryBuilder<IDocument> LQuery()
        {
            return LatestDeployment()
                .Subquery()
                .Alias("LatestDeployment")
                .Column("Id")
                .Where("Rank", UnarySqlOperand.Equal, 1);
        } 
        
        IQueryBuilder<IDocument> LatestDeployment()
        {
            var deploymentTableAlias = "d";
            var serverTaskTableAlias = "t";
            return CreateQueryBuilder<IDocument>("Deployment")
                .Alias(deploymentTableAlias)
                .InnerJoin(CreateQueryBuilder<IDocument>("ServerTask").Alias(serverTaskTableAlias))
                .On("TaskId", JoinOperand.Equal, "Id")
                .Column("Id", "Id")
                .OrderByDescending("Created")
                .AddRowNumberColumn("Rank", new ColumnFromTable("EnvironmentId", deploymentTableAlias), new ColumnFromTable("ProjectId", deploymentTableAlias))
                .Where($"NOT (({serverTaskTableAlias}.State = \'Canceled\' OR {serverTaskTableAlias}.State = \'Cancelling\') AND {serverTaskTableAlias}.StartTime IS NULL)");
        }

        [Fact]
        public void ShouldGenerateComplexStoredProcedureWithParameters()
        {
            const string eventTableAlias = "Event";

            var withJoins = CreateQueryBuilder<IDocument>("Deployment")
                .InnerJoin(CreateQueryBuilder<IDocument>("DeploymentRelatedMachine"))
                .On("Id", JoinOperand.Equal, "DeploymentId")
                .InnerJoin(CreateQueryBuilder<IDocument>("EventRelatedDocument"))
                .On("Id", JoinOperand.Equal, "RelatedDocumentId")
                .InnerJoin(CreateQueryBuilder<IDocument>("Event").Alias(eventTableAlias))
                .On("Id", JoinOperand.Equal, "EventId")
                .AllColumns()
                .OrderByDescending($"[{eventTableAlias}].[Occurred]")
                .AddRowNumberColumn("Rank", "EnvironmentId", "ProjectId", "TenantId");

            var actual = withJoins
                .Where("[DeploymentRelatedMachine].MachineId = @machineId")
                .Parameter(new Parameter("machineId", new NVarCharMax()))
                .Where("[Event].Category = \'DeploymentSucceeded\'")
                .Subquery()
                .Where("Rank = 1");

            this.Assent(actual.AsStoredProcedure("LatestSuccessfulDeploymentsToMachine"));
        }

        [Fact]
        public void ShouldGenerateFunctionWithParameters()
        {
            var packagesQuery = CreateQueryBuilder<IDocument>("NuGetPackages")
                .Where("PackageId = @packageid")
                .Parameter(new Parameter("packageid", new NVarChar(250)));

            this.Assent(packagesQuery.AsFunction("PackagesMatchingId"));
        }

        [Fact]
        public void ShouldGenerateStoredProcWithDefaultValues()
        {
            var packageIdParameter = new Parameter("packageid", new NVarChar(250));
            var query = CreateQueryBuilder<IDocument>("NuGetPackages")
                .Where("(@packageid is '') or (PackageId = @packageid)")
                .Parameter(packageIdParameter)
                .ParameterDefault(packageIdParameter, "");

            this.Assent(query.AsStoredProcedure("PackagesMatchingId"));
        }

        [Fact]
        public void ShouldGenerateFunctionWithDefaultValues()
        {
            var packageIdParameter = new Parameter("packageid", new NVarChar(250));
            var query = CreateQueryBuilder<IDocument>("NuGetPackages")
                .Where("(@packageid is '') or (PackageId = @packageid)")
                .Parameter(packageIdParameter)
                .ParameterDefault(packageIdParameter, "");

            this.Assent(query.AsFunction("PackagesMatchingId"));
        }

        [Fact]
        public void ShouldCollectParameterValuesFromSubqueriesInJoin()
        {
            CommandParameterValues parameterValues = null;
            transaction.ExecuteReader<IDocument>(Arg.Any<string>(), Arg.Do<CommandParameterValues>(pv => parameterValues = pv));

            var query = CreateQueryBuilder<IDocument>("Orders")
                .InnerJoin(CreateQueryBuilder<IDocument>("Customers")
                    .Where("Name", UnarySqlOperand.Equal, "Bob")
                    .Subquery())
                .On("CustomerId", JoinOperand.Equal, "Id");

            query.ToList();

            parameterValues.Should().Contain("Name_0", "Bob");
        }

        [Fact]
        public void ShouldCollectParametersAndDefaultsFromSubqueriesInJoin()
        {
            var parameter = new Parameter("Name", new NVarCharMax());
            var query = CreateQueryBuilder<IDocument>("Orders")
                .InnerJoin(CreateQueryBuilder<IDocument>("Customers")
                    .WhereParameterised("Name", UnarySqlOperand.Equal, parameter)
                    .ParameterDefault("Bob")
                    .Subquery())
                .On("CustomerId", JoinOperand.Equal, "Id");

            this.Assent(query.AsStoredProcedure("ShouldCollectParametersFromSubqueriesInJoin"));
        }

        [Fact]
        public void ShouldAddPaginatedQueryParameters()
        {
            CommandParameterValues parameterValues = null;
            string query = null;
            transaction.ExecuteReader<IDocument>(Arg.Do<string>(q => query = q), Arg.Do<CommandParameterValues>(pv => parameterValues = pv));

            CreateQueryBuilder<IDocument>("Orders")
                .Where("Id", UnarySqlOperand.Equal, "1")
                .ToList(10, 20);

            query.ShouldBeEquivalentTo(@"SELECT *
FROM (
    SELECT *,
    ROW_NUMBER() OVER (ORDER BY [Id]) AS RowNum
    FROM dbo.[Orders]
    WHERE ([Id] = @id_0)
) ALIAS_GENERATED_1
WHERE ([RowNum] >= @_minrow_1)
AND ([RowNum] <= @_maxrow_2)
ORDER BY [RowNum]");

            parameterValues.Count.ShouldBeEquivalentTo(3);
            parameterValues["id_0"].ShouldBeEquivalentTo("1");
            parameterValues["_minrow_1"].ShouldBeEquivalentTo(11);
            parameterValues["_maxrow_2"].ShouldBeEquivalentTo(30);
        }

        [Fact]
        public void ShouldGenerateUniqueParameterNames()
        {
            string actual = null;
            CommandParameterValues parameters = null;
            transaction.ExecuteReader<TodoItem>(Arg.Do<string>(s => actual = s),
                Arg.Do<CommandParameterValues>(p => parameters = p));

            var earlyDate = DateTime.Now;
            var laterDate = earlyDate + TimeSpan.FromDays(1);
            var query = CreateQueryBuilder<TodoItem>("Todos")
                .Where("AddedDate", UnarySqlOperand.GreaterThan, earlyDate)
                .Where("AddedDate", UnarySqlOperand.LessThan, laterDate);

            query.First();

            const string expected = @"SELECT TOP 1 *
FROM dbo.[Todos]
WHERE ([AddedDate] > @addeddate_0)
AND ([AddedDate] < @addeddate_1)
ORDER BY [Id]";

            parameters.Values.Count.ShouldBeEquivalentTo(2);
            parameters["addeddate_0"].ShouldBeEquivalentTo(earlyDate);
            parameters["addeddate_1"].ShouldBeEquivalentTo(laterDate);

            actual.ShouldBeEquivalentTo(expected);
        }

        [Fact]
        public void ShouldGenerateUniqueParameterNamesInJoin()
        {
            string actual = null;
            CommandParameterValues parameters = null;
            transaction.ExecuteReader<TodoItem>(Arg.Do<string>(s => actual = s),
                Arg.Do<CommandParameterValues>(p => parameters = p));

            var createdDate = DateTime.Now;
            var joinDate = createdDate - TimeSpan.FromDays(1);
            var sharedFieldName = "Date";

            var orders = CreateQueryBuilder<IDocument>("Orders")
                .Where(sharedFieldName, UnarySqlOperand.Equal, createdDate);


            var query = CreateQueryBuilder<TodoItem>("Customer")
                .Where(sharedFieldName, UnarySqlOperand.Equal, joinDate)
                .InnerJoin(orders.Subquery())
                .On("Id", JoinOperand.Equal, "CustomerId");

            query.First();

            const string expected =
                @"SELECT TOP 1 ALIAS_GENERATED_2.*
FROM (
    SELECT *
    FROM dbo.[Customer]
    WHERE ([Date] = @date_1)
) ALIAS_GENERATED_2
INNER JOIN (
    SELECT *
    FROM dbo.[Orders]
    WHERE ([Date] = @date_0)
) ALIAS_GENERATED_1
ON ALIAS_GENERATED_2.[Id] = ALIAS_GENERATED_1.[CustomerId]
ORDER BY ALIAS_GENERATED_2.[Id]";

            parameters.Values.Count.ShouldBeEquivalentTo(2);
            parameters["date_0"].ShouldBeEquivalentTo(createdDate);
            parameters["date_1"].ShouldBeEquivalentTo(joinDate);

            actual.ShouldBeEquivalentTo(expected);
        }

        [Fact]
        public void ShouldThrowIfDifferentNumberOfParameterValuesProvided()
        {
            CreateQueryBuilder<IDocument>("Todo")
                .WhereParameterised("Name", ArraySqlOperand.In, new[] {new Parameter("foo"), new Parameter("bar")})
                .Invoking(qb => qb.ParameterValues(new [] { "Foo" })).ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void ShouldThrowIfDifferentNumberOfParameterDefaultsProvided()
        {
            CreateQueryBuilder<IDocument>("Todo")
                .WhereParameterised("Name", ArraySqlOperand.In, new[] {new Parameter("foo"), new Parameter("bar")})
                .Invoking(qb => qb.ParameterDefaults(new [] { "Foo" })).ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void MultipleParametersInLinqQuery()
        {
            string actual = null;
            CommandParameterValues parameters = null;
            transaction.ExecuteReader<IDocument>(Arg.Do<string>(s => actual = s),
                Arg.Do<CommandParameterValues>(p => parameters = p));

            CreateQueryBuilder<IDocument>("Customers")
                .Where(d => d.Name != "Alice" && d.Name != "Bob")
                .ToList();

            const string expected = @"SELECT *
FROM dbo.[Customers]
WHERE ([Name] <> @name_0)
AND ([Name] <> @name_1)
ORDER BY [Id]";

            actual.Should().BeEquivalentTo(expected);
            parameters.Count.ShouldBeEquivalentTo(2);
            parameters.Should().Contain("name_0", "Alice");
            parameters.Should().Contain("name_1", "Bob");
        }
    }

    public class Todos
    {
        public int Id { get; set; }
        public int Completed { get; set; }
        public List<TodoItem> Items { get; set; }
    }

    public class TodoItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public bool Completed { get; set; }
    }

    public enum State
    {
        Queued,
        Running
    }
}
