- Please store all unit tests in the `stuff` folder inside the folder with `Tool.cs` with name `Test.cs`.
- When creating tests, use the `approvals testing` approach `https://approvaltests.com/`. 
  + Unit testing asserts can be difficult to use. Approval tests simplify this by taking a snapshot of the results, and confirming that they have not changed. 
  + In normal unit testing, you say assertEquals(5, person.getAge()). Approvals allow you to do this when the thing that you want to assert is no longer a primitive but a complex object. For example, you can say, Approvals.verify(person).
- Each test must contain at least one assert block that tests some part of the component under test in a text representation. 
  + This content can be prepared by a function in the test framework. 
  + Thanks to this approach, we will see a well-readable test—the “test as documentation” principle.
- Tests should contain `// given`, ``// when`, and `// then` blocks to make it clear what the test does.
- Tests names should use `should_<what-to-expect>_when_<context>`. You can add `_and|case_<some_additional>` to the test name.
- Use the xUnit framework.
- An example of such a test in Java (all logic is hidden in the `given`, `createQuestionChain`, `logger.assertLog` methods so you can see the test the main idea of what we are testing):
```java
@Test
public void shouldBuildTable_whenTwoLists_caseReplaceQuotes() {
    // given
    given("""
            [TRANSFORMER]
            name: CSV_TABLE
            type: CSV_FORMAT_TRANSFORMER
            input_keys: list1,list2
            output_key: csv_result
            template: |-
              Column1,Column2
            """);

    ChainContext context = new ChainContext() {{
        documents.put("list1", List.of("\"1\"a\"", "\"2\"a\"", "\"3\"a\"", "\"4\"a\""));
        documents.put("list2", List.of("\"1\"b\"", "\"2\"b\"", "\"3\"b\"", "\"4\"b\""));
    }};

    createQuestionChain(context);

    // when
    transformer.transform(context);

    // then
    logger.assertLog("""
            CSV_TABLE: Transformer [CSV_FORMAT_TRANSFORMER] is processing.
            CSV_TABLE: Count(null) is not integer. Got defaults(1)
            CSV_TABLE: How many times to recursively replace place-holders in prompt before getting result: 1
            CSV_TABLE: Trying to build SCV table with:
            header: Column1,Column2
            row: ["1"a", "2"a", "3"a", "4"a"]
            row: ["1"b", "2"b", "3"b", "4"b"]
            CSV_TABLE: CSV table:
            "Column1","Column2"
            "`1`a`","`1`b`"
            "`2`a`","`2`b`"
            "`3`a`","`3`b`"
            "`4`a`","`4`b`"
            
            CSV_TABLE: Transformer processed.
            """);

    assertEquals("""
                    "Column1","Column2"
                    "`1`a`","`1`b`"
                    "`2`a`","`2`b`"
                    "`3`a`","`3`b`"
                    "`4`a`","`4`b`"
                    """,
            context.getValue("csv_result"));
}
```
- When creating a test, I don't want to print to the console, I want an assert that compares object.toString with the actual hardcoded expected value in the test.
- When working with tests, follow an iterative approach:
  + Create a list of actions, where each test is one task.
  + Start with the first test on the list and do everything that is requested.
  + Make sure it works. 
  + If there was a change in the tested functionality, run all tests to make sure nothing is broken.
  + After that, you can take the next task from the list. 
- Run all tests to make sure everything works correctly: 
  + `dotnet test run.csproj --verbosity minimal` run all tests for the tool
  + `dotnet test run.csproj --filter "FullyQualifiedName~GetToolInfo_ReturnsCorrectInformation" --verbosity normal` run single test
  + `dotnet test run.csproj --filter "Name~GetToolInfo" --verbosity normal` run single test for short name
  + `dotnet test run.csproj --filter "ClassName~Tests" --verbosity normal` run tests by class name
  + `dotnet test run.csproj --list-tests` list all tests
