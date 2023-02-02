
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

    const object = await res.json();

    PopUp(object["message"]);

    if(object["status"] == 200)
    {
        localStorage.setItem("accessToken", object["accessToken"]);
        window.location.assign("/");
    }
}

document.getElementById("loginForm").addEventListener("submit", Login);