
# Twitch Azure TTS

### What?

Application that use Azure Speech Service to read messages from Twitch Chat

![First Open](/TwitchAzureTTS/images/FirstOpen.png)

### How to use?

#### Navigation

Use Arrows to navigate, Spacebar to select, Escape to go back

When inputing use Enter to submit

#### Joining channel

1. Select `Login`
2. Authorize permissions to read Chat
3. Wait until all redirects will be done
4. You safe to close tab when it say so, tho tab may inform you about errors
5. Select `Set Channel` and enter channel that you want to join
6. Select `Connect`

In Log section of the app you will see that chat joined or error message

#### Setting up Azure Speech Service

1. You need to sign up to [Microsoft Azure](https://azure.microsoft.com/en-us/). Note that you will need to enter your bank card information to register. However, Azure Speech Service will be free for you if you syntenize less than 500 000 characters per month
2. Go to [Azure Portal](https://portal.azure.com)
3. Select `Create a resource`
4. Select `AI + Machine Learning` > `Speech` > `Create`
5. Create your new resource, it might take few minutes to create resource
6. Open your resource and select `Keys and Endpoint`
7. Copy `Key1`
8. Come back to TwitchAzureTTS, select `Set Key` and paste your Key
9. Do the same for `Location/Region` and `Set Region`
10. Select `Set Default Voice` and set up voice

Don't forget to send message to your chat to test if everything is working ;)

#### Setting up Azure Metrics

1. Go to [Azure Portal](https://portal.azure.com)
2. Open `All services`
3. In `Identity` open `Azure Active Directory`
4. Copy `Tenant ID`
5. In TwitchAzureTTS select `Set Tenant ID` and paste your ID
6. If the browser appears, authorize the application to continue
7. If you have multiple resources, select one that you want to track

