module FsCrawler.Tests

open FsCrawler
open FsCrawler.Client
open NUnit.Framework

[<Test>]
let ``Can run against mocks`` () =
    let startUrl = Url "http://test.com"
    let childPages = [ Url "http://test.com/1"; Url "http://test.com/2" ]
    let fetch url =
        async {
            return
                {
                    Url = url
                    Session = CrawlSession(System.Guid.NewGuid())
                    Headers = []
                    Content = [||]
                }
        }
    let dependencies =
        {
            Fetch = fetch
            Config =
                {
                    DegreeOfParallelism = 2
                    GetNextPages =
                        function
                        | crawl when crawl.Url = startUrl -> childPages
                        | _ -> []
                    StoreCrawl = fun _ -> async { return () }
                }
        }
    let urlsFetched = Client.fetchRecursiveInternal dependencies startUrl |> Async.RunSynchronously
    Assert.AreEqual(Set.ofList (startUrl :: childPages), urlsFetched)
