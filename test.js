
const apiUrl = "http://localhost:8080/";

function SendApiCall(requestType, url, body, async)
{
    const request = new XMLHttpRequest();
    request.open(requestType, url, async);
    request.setRequestHeader("Accept", "application/json");
    request.setRequestHeader("Content-Type", "application/json");
    request.send(body);
    return {
        "code" : request.status,
        "body": request.responseText
    };
}

function PcStarterGet(url)
{
    return SendApiCall("GET", url, null, false);
}

function PcStarterPost(url, status)
{
    const body = `{"status" : ${status}}`;
    
    return SendApiCall("POST", url, body, false);
}

function Test()
{
    alert("Button pressed!!");
}
const pcbg = document.getElementById("pcButtonGet");

pcbg.addEventListener("click", function(){
    const response = PcStarterGet(apiUrl+"pcStarter"); 
    const obj =JSON.parse(response["body"]);
    const status = obj["status"];
    if(status == 1)
    {
        pcbg.textContent = 1;
        pcbg.style.backgroundColor = "green";
    }
    else if(status == 0)
    {
        pcbg.textContent = 0;
        pcbg.style.backgroundColor = "red";
    }
    else
        pcbg.textContent = "error";

    alert(response["body"]);
});


const pcbp = document.getElementById("pcButtonPost");
//res = PcStarterGet(apiUrl+"pcStarter");

let st = 0;
pcbp.addEventListener("click", function(){
    if(st == 0){st = 1;}else{st = 0;} 
    //console.log(st);
    const response = PcStarterPost(apiUrl+"pcStarter", st); 
    alert(response["body"]);
});