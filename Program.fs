module FsharpNamedHttpClient.App

open System
open System.IO
open System.Net.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe

let poc (id: string) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let factory = ctx.GetService<IHttpClientFactory>()
            let httpClient = factory.CreateClient("poc")
            match httpClient.BaseAddress |> Option.ofObj with
            | Some baseAddress ->
                return! text $"Poc {id} \n {baseAddress} (as expected)" next ctx
            | None ->
                return! text $"Poc {id} \n null (expected a URL)" next ctx
        }

let workaround (id: string) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let factory = ctx.GetService<IHttpClientFactory>()
            let httpClient = factory.CreateClient("workaround")
            match httpClient.BaseAddress |> Option.ofObj with
            | Some baseAddress ->
                let asString = baseAddress.ToString()
                if asString.Contains("microsoft") then
                    return! text $"Poc {id} \n {baseAddress.ToString()} (as expected)" next ctx
                else
                    return! text $"Poc {id} \n {baseAddress.ToString()} (this is weird)" next ctx
            | None ->
                return! text $"Poc {id} \n null (expected a URL)" next ctx
        }

let workaround2 (id: string) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let factory = ctx.GetService<IHttpClientFactory>()
            let httpClient = factory.CreateClient("workaround2")
            match httpClient.BaseAddress |> Option.ofObj with
            | Some baseAddress ->
                let asString = baseAddress.ToString()
                if asString.Contains("github") then
                    return! text $"Poc {id} \n {baseAddress.ToString()} (as expected)" next ctx
                else
                    return! text $"Poc {id} \n {baseAddress.ToString()} (this is weird)" next ctx
            | None ->
                return! text $"Poc {id} \n null (expected a URL)" next ctx
        }

let webApp =
    choose
        [ GET
          >=> choose
              [ route "/" >=> redirectTo false "/poc/demo"
                routef "/poc/%s" poc
                routef "/workaround/%s" workaround
                routef "/workaround2/%s" workaround2
                ]
          setStatusCode 404 >=> text "Not Found" ]

let errorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

let configureApp (app: IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()

    (match env.IsDevelopment() with
     | true -> app.UseDeveloperExceptionPage()
     | false -> app.UseGiraffeErrorHandler(errorHandler).UseHttpsRedirection())
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services: IServiceCollection) =
    services.AddCors().AddGiraffe() |> ignore

    services.AddHttpClient(
        "poc",
        (fun httpClient -> httpClient.BaseAddress <- Uri("https://example.com")))
    |> ignore

    services.AddHttpClient<HttpClient>(
        "workaround",
        (fun httpClient -> httpClient.BaseAddress <- Uri("https://microsoft.com"))
    )
    |> ignore

    services.AddHttpClient<HttpClient>(
        "workaround2",
        (fun httpClient -> httpClient.BaseAddress <- Uri("https://github.com"))
    )
    |> ignore

let configureLogging (builder: ILoggingBuilder) =
    builder.AddConsole().AddDebug() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()

    Host
        .CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .UseContentRoot(contentRoot)
                .Configure(Action<IApplicationBuilder> configureApp)
                .ConfigureServices(configureServices)
                .ConfigureLogging(configureLogging)
            |> ignore)
        .Build()
        .Run()

    0
