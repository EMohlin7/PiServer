
async function CreateAccount(event)
{
    event.preventDefault();
    PopUp("Creating account...");
    const form = event.currentTarget;
    const formData = new FormData(form);
    const data = Object.fromEntries(formData.entries());
    
    if(data["password"] != data["confirmPassword"])
    {
        PopUp("The passwords have to match");
        return;
    }
    const res = await fetch(host + "/newaccount", {
        method: "post",
        body: JSON.stringify(data),
        headers: {"Content-Type":"application/json"}
    });

    PopUp(res.status);
    if(res.redirected)
    {
        window.location.assign(res.url);
        PopUp("account created");   
    }
}


document.getElementById("accountForm").addEventListener("submit", CreateAccount);