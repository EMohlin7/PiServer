

async function PcStarterGet()
{
    return await ApiGet(host + "/pcStarter");
}

function PcStarterPost(url, status)
{
    const body = `{"status" : ${status}}`;
    
    return SendApiCall("POST", url, body, false);
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

    //alert(body);
    console.log(body);
});



async function Login(event)
{
    event.preventDefault();
    PopUp("Logging in...")
    const form = event.currentTarget;
    const formData = new FormData(form);
    const obj = Object.fromEntries(formData.entries());
    const res = await fetch(host + "/login", {
        method: "post",
        body: JSON.stringify(obj),
        headers: {"Content-Type":"application/json"}
    });

    PopUp(res.status);

    if(res.redirected)
    {
        //window.location.assign(res.url);
        PopUp("Login");
        
    }
}

document.getElementById("loginForm").addEventListener("submit", Login);