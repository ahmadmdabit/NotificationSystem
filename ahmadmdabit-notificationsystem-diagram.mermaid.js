flowchart TB
    subgraph "UI (Web MVC)"
        UI["UI (Web MVC)"]:::ui
    end

    subgraph "API Gateway (Ocelot)"
        AGW["API Gateway"]:::apiGw
    end

    subgraph "Microservices"
        subgraph "UserService"
            US["UserService"]:::service
        end
        subgraph "NotificationService"
            NS["NotificationService"]:::service
        end
    end

    subgraph "Shared Libraries"
        API_Lib["API Library"]:::libs
        BLL["BLL Library"]:::libs
        DAL["DAL Library"]:::libs
        COMMON["Common Library"]:::libs
    end

    subgraph "Databases"
        UserDB["UserDB.mdf"]:::db
        NotificationDB["NotificationDB.mdf"]:::db
    end

    UI --> AGW
    AGW --> US
    AGW --> NS
    US --> API_Lib
    US --> BLL
    US --> DAL
    US --> COMMON
    NS --> API_Lib
    NS --> BLL
    NS --> DAL
    NS --> COMMON
    US --> UserDB
    NS --> NotificationDB

    click UI "https://github.com/ahmadmdabit/notificationsystem/tree/master/UI/"
    click AGW "https://github.com/ahmadmdabit/notificationsystem/tree/master/ApiGateway/"
    click US "https://github.com/ahmadmdabit/notificationsystem/tree/master/UserService/"
    click NS "https://github.com/ahmadmdabit/notificationsystem/tree/master/NotificationService/"
    click API_Lib "https://github.com/ahmadmdabit/notificationsystem/tree/master/API/"
    click BLL "https://github.com/ahmadmdabit/notificationsystem/tree/master/BLL/"
    click DAL "https://github.com/ahmadmdabit/notificationsystem/tree/master/DAL/"
    click COMMON "https://github.com/ahmadmdabit/notificationsystem/tree/master/Common/"
    click UserDB "https://github.com/ahmadmdabit/notificationsystem/blob/master/UserService/App_Data/UserDB.mdf"
    click NotificationDB "https://github.com/ahmadmdabit/notificationsystem/blob/master/NotificationService/App_Data/NotificationDB.mdf"

    classDef ui fill:#D6EAF8,stroke:#1F618D
    classDef apiGw fill:#E8DAEF,stroke:#6C3483
    classDef service fill:#D5F5E3,stroke:#239B56
    classDef libs fill:#E5E7E9,stroke:#7B7D7D
    classDef db fill:#FCF3CF,stroke:#B7950B