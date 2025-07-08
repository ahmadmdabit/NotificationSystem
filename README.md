# NotificationSystem 

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/ahmadmdabit/NotificationSystem)

## Diagram 

[![Interactive Diagram](https://raster.shields.io/badge/Interactive_Diagram-lightgreen.png?logoColor=eeeeee&logo=data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAMAAAAoLQ9TAAAAzFBMVEUAAACTM+qTM+mTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+pYr7W1AAAAQ3RSTlMAAAAlZGhpWxQEBajeV3QHCsHcYO6ABgm/3V75/oTtnJ7TVIqWjivDzJWXcs8cy8CHbPvrwqIIXQHKJyiZJinO0P3jWa9vVAAAAKRJREFUGNNVz+kSgiAYhWFI2zRTWtCKtJ2sLG3fM7n/ewqBpun9+czwzQEA+AuIClDTi6VypfoVaJiMsZpVt5VAB3FoNFttLAW6HodOt0f6jgLkB4PhaDyZzhRQatvePAwXS6xgFUVr/oxRR8KGxTHJwXMVkCTZ7oLARwr2B3w8WWdMqQJ0ud7M++P5UsCHpSl7ZxlB8qiYLjINAfnnRLomh/0FPrSFFcj8a3ouAAAAAElFTkSuQmCC)](https://gitdiagram.com/ahmadmdabit/NotificationSystem)

![The project's diagram](ahmadmdabit-notificationsystem-diagram.png)

## Project Structure: 
* Common 
  * Common Library .NET Stadard 2.0 
  * DAL Library .NET Stadard 2.0 (Microsoft SQL Server (Local mdf), Dapper)
  * BLL Library .NET Stadard 2.0 
  * API Library .NET Stadard 2.0 (Swagger)

* Services 
  * UserService ASP.NET Core 3.1 Web Api RESTFul 
  * NotificationService ASP.NET Core 3.1 Web Api RESTFul 

* ApiGatway (Ocelot)

* UI 
  * UI ASP.NET Core 3.1 Web MVC (RestSharp)

## Database: Microsoft SQL Server (Local mdf) 
* DB1: Tables: Users  
* DB2: Tables: Notifications, NotificationHistories, SPs: SP_NotificationHistory_I, Types: Type_NotificationHistory 

## Web Services: C# ASP.NET Core 3.1 N-Tier  
* Two Micro-Services: UserService, NotificationService 
* ApiGatway 

## UI: C# ASP.NET Core 3.1 Web MVC / HTML5, CSS3, JS, jQuery, Bootstrap 
* Pages: Notifications List, Notification Add 
* Note: Using all services by ajax 

## License

Licensed under the [MIT license](LICENSE.md).
