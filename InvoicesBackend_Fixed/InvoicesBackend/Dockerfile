FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY InvoiceTemplateEngine ./InvoiceTemplateEngine
COPY InvoicesBackend ./InvoicesBackend
WORKDIR /src/InvoicesBackend
RUN dotnet publish InvoicesBackend.csproj -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "InvoicesBackend.dll"]