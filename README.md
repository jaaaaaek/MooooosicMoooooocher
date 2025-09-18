<h1>Hello!</h1>

Disclaimer! You need a SoundCloud Go+ membership for this to work, due to the need to authenticate with them when pulling songs from their api :(

This tool takes links to soundcloud songs like:

https://soundcloud.com/rick-astley-official/never-gonna-give-you-up-4
https://soundcloud.com/warnerrecords/heart-of-glass1

And formats them like this! So they can be downloaded via yt-dlp. A "A feature-rich command-line audio/video downloader" https://github.com/yt-dlp/yt-dlp

yt-dlp -f ba --extract-audio --audio-format wav https://soundcloud.com/rick-astley-official/never-gonna-give-you-up-4 --add-header "Authorization: OAuth <yourauthtokenhere>" --extractor-retries 10 --retry-sleep extractor:300

yt-dlp -f ba --extract-audio --audio-format wav https://soundcloud.com/warnerrecords/heart-of-glass1 --add-header "Authorization: OAuth <yourauthtokenhere>" --extractor-retries 10 --retry-sleep extractor:300

<h3>The only thing you need for this is an authorization token from soundcloud. </h3>
Which is a bit! Tricky to get. (Not really)

Just go to SoundCloud.com, use Inspect Element to navigate to the network tab as shown below, and paste the following into the filter:
me?client_id=

Now refresh the page and you should see something like this:
<img width="846" height="421" alt="authtokenguide" src="https://github.com/user-attachments/assets/cc4804da-0b10-418a-947d-c3e9cce49b90" />

Now use that token as the value for the SoundCloudToken parameter when running this program. 

Place all links you want to download in the NewDownloads.txt folder
And then if you want, put those links in the ExistingDownloads folder as well to mark them as downloaded so they aren't pulled again in future runs of this.

<h1>Now that that's done and you have formatted links</h1>
Navigate to the "Music" folder within this project on the command line, and then paste those links. The yt-dlp.exe is already in there so it should look something like this 

<img width="846" height="421" alt="image" src="https://github.com/user-attachments/assets/a2055ff1-68fb-4bd2-aa89-1019649ea882" />
Congrats! You should now see the downloaded song in the "Music" directory. 
<h1>Good luck!</h1><
