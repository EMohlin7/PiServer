

async function PcStarterGet()
{
    return await fetch(host + "/pcStarter");
}

function PcStarterPost(url, status)
{
    const body = `{"status" : ${status}}`;
    
    return SendApiCall("POST", url, body, false);
}


const pcbg = document.getElementById("pcButtonGet");

pcbg.addEventListener("click", async function(){
    const res = await PcStarterGet();
    const data = await res.json()
    const status = data["status"];
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

    //alert(body);
    console.log(data);
});





