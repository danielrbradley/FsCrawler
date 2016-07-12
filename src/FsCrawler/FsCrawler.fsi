namespace FsCrawler

type Url = Url of string

type ContentHash = ContentHash of byte[]

type CrawlSession = CrawlSession of System.Guid

type Crawl =
    {
        Session : CrawlSession
        Url : Url
        Headers : (string * string) list
        Content : byte[]
    }

type Config =
    {
        StoreCrawl : Crawl -> Async<unit>
        GetNextPages : Crawl -> Url list
        DegreeOfParallelism : int
    }

module Client =
    type Dependencies =
        {
            Fetch : Url -> Async<Crawl>
            Config : Config
        }

    val internal fetchRecursiveInternal : Dependencies -> Url -> Async<Url Set>
    val fetchRecursive : Config -> Url -> Async<Url Set>
