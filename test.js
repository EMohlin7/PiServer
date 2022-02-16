
const host = window.location.protocol + "//" + window.location.host;

async function ApiGet(url)
{
    const res = await fetch(url);
    return await res.text();
}

async function PcStarterGet()
{
    return await JSON.parse(ApiGet(host + "/pcStarter"));
}

function PcStarterPost(url, status)
{
    const body = `{"status" : ${status}}`;
    
    return SendApiCall("POST", url, body, false);
}

function test(form)
{
    form.action = "login";
    alert("test");
}


const pcbg = document.getElementById("pcButtonGet");

pcbg.addEventListener("click", async function(){
    const body = await PcStarterGet();
    const status = body["status"];
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